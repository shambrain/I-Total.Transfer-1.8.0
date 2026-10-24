using Microsoft.Data.Sqlite;
using ITotalTransfer.Core;

namespace ITotalTransfer.Services;

/// <summary>
/// SQLite-backed durable queue history. Unlike the previous per-operation open/close tracker, this
/// keeps a single WAL connection for the session and writes file updates inside transactions, so a
/// 26,000-file library does not perform 26,000 connection handshakes. JSON remains the portable,
/// human-readable session snapshot.
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
        _log.Write("SQLite schema verified (Sessions/Files/Events, WAL, indexed state).");
    }

    public void BeginSession(string sessionId, string deviceName, string destination, string status)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "INSERT INTO Sessions(SessionId,DeviceName,Destination,StartedUtc,UpdatedUtc,Status) VALUES($id,$device,$dest,$now,$now,$status) ON CONFLICT(SessionId) DO UPDATE SET UpdatedUtc=$now, Status=$status;";
        command.Parameters.AddWithValue("$id", sessionId);
        command.Parameters.AddWithValue("$device", deviceName);
        command.Parameters.AddWithValue("$dest", destination);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$status", status);
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
            INSERT INTO Files(Destination,RelativePath,ExpectedBytes,State,Attempts,Error,UpdatedUtc)
            VALUES($dest,$path,$size,$state,$attempts,$error,$updated)
            ON CONFLICT(Destination,RelativePath) DO UPDATE SET
                ExpectedBytes=excluded.ExpectedBytes, State=excluded.State, Attempts=excluded.Attempts,
                Error=excluded.Error, UpdatedUtc=excluded.UpdatedUtc;
            """;
        SqliteParameter dest = command.Parameters.Add("$dest", SqliteType.Text);
        SqliteParameter path = command.Parameters.Add("$path", SqliteType.Text);
        SqliteParameter size = command.Parameters.Add("$size", SqliteType.Integer);
        SqliteParameter state = command.Parameters.Add("$state", SqliteType.Text);
        SqliteParameter attempts = command.Parameters.Add("$attempts", SqliteType.Integer);
        SqliteParameter error = command.Parameters.Add("$error", SqliteType.Text);
        SqliteParameter updated = command.Parameters.Add("$updated", SqliteType.Text);

        foreach (TransferEntry entry in entries)
        {
            dest.Value = destination;
            path.Value = entry.RelativePath;
            size.Value = Math.Max(0, entry.ExpectedBytes);
            state.Value = entry.State.ToString();
            attempts.Value = entry.Attempts;
            error.Value = (object?)entry.Error ?? DBNull.Value;
            updated.Value = entry.UpdatedUtc.ToString("O");
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

    public void Dispose()
    {
        try { _connection?.Close(); _connection?.Dispose(); } catch { }
        _connection = null;
    }
}

public sealed record SessionHistoryRow(string SessionId, string DeviceName, string Destination, string StartedUtc, string UpdatedUtc, string Status);
public sealed record FileHistoryRow(string RelativePath, long ExpectedBytes, string State, int Attempts, string? Error);
