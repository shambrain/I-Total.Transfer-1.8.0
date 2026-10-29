using Microsoft.Data.Sqlite;
using ITotalTransfer.Core;

namespace ITotalTransfer.Services;

/// <summary>
/// SQLite-backed durable queue history. Unlike the previous per-operation open/close tracker, this
/// keeps a single WAL connection for the session and writes file updates inside transactions, so a
/// 26,000-file library does not perform 26,000 connection handshakes. JSON remains the portable,
/// human-readable session snapshot.
///
/// <para>
/// The schema is migrated in place: a database written by an earlier 1.8.0 build is opened, missing
/// columns are added with their defaults, and existing rows are preserved. Nothing is dropped and no
/// table is recreated, so a queue that is mid-recovery survives an application upgrade.
/// </para>
/// </summary>
public sealed class SessionDatabase : IDisposable
{
    private readonly string _connectionString;
    private readonly LogService _log;
    private SqliteConnection? _connection;

    public SessionDatabase(string databasePath, LogService log)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        _log = log;
    }

    private SqliteConnection Connection => _connection ??= Open();

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Initialize()
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            PRAGMA busy_timeout=5000;
            CREATE TABLE IF NOT EXISTS Sessions (
                SessionId TEXT PRIMARY KEY,
                DeviceName TEXT NOT NULL,
                Destination TEXT NOT NULL,
                StartedUtc TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL,
                Status TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Files (
                Destination TEXT NOT NULL,
                RelativePath TEXT NOT NULL,
                ExpectedBytes INTEGER NOT NULL,
                State TEXT NOT NULL,
                Attempts INTEGER NOT NULL,
                Error TEXT NULL,
                UpdatedUtc TEXT NOT NULL,
                PRIMARY KEY (Destination, RelativePath)
            );
            CREATE TABLE IF NOT EXISTS Events (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CreatedUtc TEXT NOT NULL,
                Level TEXT NOT NULL,
                Message TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Files_State ON Files(Destination, State);
            """;
        command.ExecuteNonQuery();
        Migrate();
        _log.Write("SQLite schema verified (Sessions/Files/Events, WAL, indexed state).");
    }

    /// <summary>
    /// Adds the columns introduced after the first 1.8.0 schema. Adding a column keeps every existing
    /// row and is idempotent, which matters because an interrupted upgrade may run this twice.
    /// </summary>
    private void Migrate()
    {
        AddColumnIfMissing("Files", "SizeKnown", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing("Files", "BytesWritten", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing("Files", "SourceSha256", "TEXT NULL");
        AddColumnIfMissing("Files", "StagedPayloadSha256", "TEXT NULL");
        AddColumnIfMissing("Files", "DestinationSha256", "TEXT NULL");
        AddColumnIfMissing("Files", "Verification", "TEXT NOT NULL DEFAULT 'None'");
        AddColumnIfMissing("Files", "SourceModifiedUtc", "TEXT NULL");
        AddColumnIfMissing("Files", "SourceChanged", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing("Files", "Outcome", "TEXT NOT NULL DEFAULT 'None'");
        AddColumnIfMissing("Files", "CommittedPath", "TEXT NULL");
        AddColumnIfMissing("Sessions", "BackendName", "TEXT NULL");
        AddColumnIfMissing("Sessions", "DeviceFamily", "TEXT NULL");
        AddColumnIfMissing("Sessions", "EnumerationStatus", "TEXT NULL");
    }

    private void AddColumnIfMissing(string table, string column, string definition)
    {
        if (HasColumn(table, column)) return;
        using var alter = Connection.CreateCommand();
        // Table and column names are constants in this file, never user input; the definition is fixed.
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
        _log.Write($"SQLite schema migrated: added {table}.{column}.", "INFO");
    }

    private bool HasColumn(string table, string column)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public void BeginSession(string sessionId, string deviceName, string destination, string status,
        string? backendName = null, string? deviceFamily = null, string? enumerationStatus = null)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Sessions(SessionId,DeviceName,Destination,StartedUtc,UpdatedUtc,Status,BackendName,DeviceFamily,EnumerationStatus)
            VALUES($id,$device,$dest,$now,$now,$status,$backend,$family,$enumeration)
            ON CONFLICT(SessionId) DO UPDATE SET
                UpdatedUtc=$now, Status=$status, BackendName=$backend, DeviceFamily=$family,
                EnumerationStatus=$enumeration;
            """;
        command.Parameters.AddWithValue("$id", sessionId);
        command.Parameters.AddWithValue("$device", deviceName);
        command.Parameters.AddWithValue("$dest", destination);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$backend", (object?)backendName ?? DBNull.Value);
        command.Parameters.AddWithValue("$family", (object?)deviceFamily ?? DBNull.Value);
        command.Parameters.AddWithValue("$enumeration", (object?)enumerationStatus ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void UpsertFile(string destination, TransferEntry entry) => UpsertFiles(destination, [entry]);

    /// <summary>Upserts a batch of file states inside one transaction.</summary>
    public void UpsertFiles(string destination, IEnumerable<TransferEntry> entries)
    {
        using var transaction = Connection.BeginTransaction();
        using var command = Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Files(Destination,RelativePath,ExpectedBytes,State,Attempts,Error,UpdatedUtc,
                               SizeKnown,BytesWritten,SourceSha256,StagedPayloadSha256,DestinationSha256,
                               Verification,SourceModifiedUtc,SourceChanged,Outcome,CommittedPath)
            VALUES($dest,$path,$size,$state,$attempts,$error,$updated,
                   $sizeKnown,$written,$source,$staged,$destinationHash,
                   $verification,$modified,$changed,$outcome,$committedPath)
            ON CONFLICT(Destination,RelativePath) DO UPDATE SET
                ExpectedBytes=excluded.ExpectedBytes, State=excluded.State, Attempts=excluded.Attempts,
                Error=excluded.Error, UpdatedUtc=excluded.UpdatedUtc, SizeKnown=excluded.SizeKnown,
                BytesWritten=excluded.BytesWritten, SourceSha256=excluded.SourceSha256,
                StagedPayloadSha256=excluded.StagedPayloadSha256, DestinationSha256=excluded.DestinationSha256,
                Verification=excluded.Verification, SourceModifiedUtc=excluded.SourceModifiedUtc,
                SourceChanged=excluded.SourceChanged, Outcome=excluded.Outcome, CommittedPath=excluded.CommittedPath;
            """;
        SqliteParameter dest = command.Parameters.Add("$dest", SqliteType.Text);
        SqliteParameter path = command.Parameters.Add("$path", SqliteType.Text);
        SqliteParameter size = command.Parameters.Add("$size", SqliteType.Integer);
        SqliteParameter state = command.Parameters.Add("$state", SqliteType.Text);
        SqliteParameter attempts = command.Parameters.Add("$attempts", SqliteType.Integer);
        SqliteParameter error = command.Parameters.Add("$error", SqliteType.Text);
        SqliteParameter updated = command.Parameters.Add("$updated", SqliteType.Text);
        SqliteParameter sizeKnown = command.Parameters.Add("$sizeKnown", SqliteType.Integer);
        SqliteParameter written = command.Parameters.Add("$written", SqliteType.Integer);
        SqliteParameter source = command.Parameters.Add("$source", SqliteType.Text);
        SqliteParameter staged = command.Parameters.Add("$staged", SqliteType.Text);
        SqliteParameter destinationHash = command.Parameters.Add("$destinationHash", SqliteType.Text);
        SqliteParameter verification = command.Parameters.Add("$verification", SqliteType.Text);
        SqliteParameter modified = command.Parameters.Add("$modified", SqliteType.Text);
        SqliteParameter changed = command.Parameters.Add("$changed", SqliteType.Integer);
        SqliteParameter outcome = command.Parameters.Add("$outcome", SqliteType.Text);
        SqliteParameter committedPath = command.Parameters.Add("$committedPath", SqliteType.Text);

        foreach (TransferEntry entry in entries)
        {
            dest.Value = destination;
            path.Value = entry.RelativePath;
            size.Value = Math.Max(0, entry.ExpectedBytes);
            state.Value = entry.State.ToString();
            attempts.Value = entry.Attempts;
            error.Value = (object?)entry.Error ?? DBNull.Value;
            updated.Value = entry.UpdatedUtc.ToString("O");
            sizeKnown.Value = entry.SizeKnown ? 1 : 0;
            written.Value = Math.Max(0, entry.BytesWritten);
            source.Value = (object?)entry.SourceSha256 ?? DBNull.Value;
            staged.Value = (object?)entry.StagedPayloadSha256 ?? DBNull.Value;
            destinationHash.Value = (object?)entry.DestinationSha256 ?? DBNull.Value;
            verification.Value = entry.Verification.ToString();
            modified.Value = entry.SourceModifiedUtc is { } stamp ? stamp.ToString("O") : (object)DBNull.Value;
            changed.Value = entry.SourceChanged ? 1 : 0;
            outcome.Value = entry.Outcome.ToString();
            committedPath.Value = (object?)entry.CommittedPath ?? DBNull.Value;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void AddEvent(string level, string message)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "INSERT INTO Events(CreatedUtc,Level,Message) VALUES($created,$level,$message);";
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$level", level);
        command.Parameters.AddWithValue("$message", message);
        command.ExecuteNonQuery();
    }

    public void EndSession(string sessionId, string status)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "UPDATE Sessions SET UpdatedUtc=$now, Status=$status WHERE SessionId=$id;";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$id", sessionId);
        command.ExecuteNonQuery();
    }

    /// <summary>Recent sessions for the History screen.</summary>
    public IReadOnlyList<SessionHistoryRow> QueryRecentSessions(int limit = 50)
    {
        var rows = new List<SessionHistoryRow>();
        using var command = Connection.CreateCommand();
        command.CommandText = "SELECT SessionId,DeviceName,Destination,StartedUtc,UpdatedUtc,Status FROM Sessions ORDER BY UpdatedUtc DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add(new SessionHistoryRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return rows;
    }

    /// <summary>Non-success file rows for the History/Recovery screen.</summary>
    public IReadOnlyList<FileHistoryRow> QueryProblemFiles(string destination, int limit = 500)
    {
        var rows = new List<FileHistoryRow>();
        using var command = Connection.CreateCommand();
        command.CommandText = """
            SELECT RelativePath,ExpectedBytes,State,Attempts,Error FROM Files
            WHERE Destination=$dest AND State NOT IN ('IntegrityVerified','SizeVerified','Skipped')
            ORDER BY State, RelativePath LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$dest", destination);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add(new FileHistoryRow(reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        return rows;
    }

    /// <summary>
    /// The persisted state of every planned file, including the hashes and the verification grade.
    /// Used by tests and by any recovery/report path that must not depend on the JSON manifest alone.
    /// </summary>
    public IReadOnlyList<PersistedFileState> QueryFileStates(string destination)
    {
        var rows = new List<PersistedFileState>();
        using var command = Connection.CreateCommand();
        command.CommandText = """
            SELECT RelativePath,ExpectedBytes,SizeKnown,BytesWritten,State,Attempts,Error,
                   SourceSha256,StagedPayloadSha256,DestinationSha256,Verification,SourceModifiedUtc,SourceChanged,Outcome,CommittedPath
            FROM Files WHERE Destination=$dest ORDER BY RelativePath;
            """;
        command.Parameters.AddWithValue("$dest", destination);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new PersistedFileState(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetInt64(2) != 0,
                reader.GetInt64(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetInt64(12) != 0,
                reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14)));
        }
        return rows;
    }

    public void Dispose()
    {
        try { _connection?.Close(); _connection?.Dispose(); } catch { }
        _connection = null;
    }
}

public sealed record SessionHistoryRow(string SessionId, string DeviceName, string Destination, string StartedUtc, string UpdatedUtc, string Status);
public sealed record FileHistoryRow(string RelativePath, long ExpectedBytes, string State, int Attempts, string? Error);

/// <summary>One persisted file row, hashes and verification included.</summary>
public sealed record PersistedFileState(string RelativePath, long ExpectedBytes, bool SizeKnown, long BytesWritten,
    string State, int Attempts, string? Error, string? SourceSha256, string? StagedPayloadSha256,
    string? DestinationSha256, string Verification, string? SourceModifiedUtc, bool SourceChanged, string Outcome,
    string? CommittedPath);
