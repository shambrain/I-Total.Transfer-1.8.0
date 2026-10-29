using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The SQLite queue is the durable record a recovery or report path may read without the JSON
/// manifest. These tests pin the schema migration (an older database keeps its rows and gains the
/// hash/verification columns) and the round-trip of every field the engine records.
/// </summary>
[TestClass]
public sealed class SessionDatabaseTests
{
    private static string TempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-db", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static TransferEntry Entry(string relativePath) => new()
    {
        RelativePath = relativePath,
        ExpectedBytes = 1234,
        SizeKnown = true,
        BytesWritten = 1200,
        State = TransferState.IntegrityVerified,
        Attempts = 2,
        SourceSha256 = "AA11",
        StagedPayloadSha256 = "BB22",
        DestinationSha256 = "CC33",
        Verification = VerificationLevel.SourceAndDestinationHash,
        SourceModifiedUtc = DateTimeOffset.Parse("2026-10-20T10:00:00Z"),
        SourceChanged = true,
        UpdatedUtc = DateTimeOffset.Parse("2026-10-29T10:00:00Z")
    };

    [TestMethod]
    public void AFileRowRoundTripsEveryHashAndTheVerificationGrade()
    {
        string root = TempRoot();
        try
        {
            string dbPath = Path.Combine(root, ".i-total", "session.db");
            using var database = new SessionDatabase(dbPath, new LogService(root));
            database.Initialize();
            database.Initialize(); // idempotent: a second open must not fail or duplicate columns
            database.BeginSession("session-1", "iPhone", root, "RUNNING", "Windows Portable Devices (native)",
                "Apple", "Complete");
            database.UpsertFile(root, Entry("DCIM\\IMG_0001.HEIC"));

            PersistedFileState row = database.QueryFileStates(root).Single();
            Assert.AreEqual("DCIM\\IMG_0001.HEIC", row.RelativePath);
            Assert.AreEqual(1234, row.ExpectedBytes);
            Assert.IsTrue(row.SizeKnown);
            Assert.AreEqual(1200, row.BytesWritten);
            Assert.AreEqual(nameof(TransferState.IntegrityVerified), row.State);
            Assert.AreEqual(2, row.Attempts);
            Assert.AreEqual("AA11", row.SourceSha256);
            Assert.AreEqual("BB22", row.StagedPayloadSha256);
            Assert.AreEqual("CC33", row.DestinationSha256);
            Assert.AreEqual(nameof(VerificationLevel.SourceAndDestinationHash), row.Verification);
            Assert.AreEqual("2026-10-20T10:00:00.0000000+00:00", row.SourceModifiedUtc);
            Assert.IsTrue(row.SourceChanged);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void AnUnknownSizeIsPersistedAsUnknownNotAsZero()
    {
        string root = TempRoot();
        try
        {
            string dbPath = Path.Combine(root, ".i-total", "session.db");
            using var database = new SessionDatabase(dbPath, new LogService(root));
            database.Initialize();
            var entry = Entry("DCIM\\IMG_0002.JPG");
            entry.SizeKnown = false;
            entry.ExpectedBytes = 0;
            entry.BytesWritten = 512;
            database.UpsertFile(root, entry);

            PersistedFileState row = database.QueryFileStates(root).Single();
            Assert.IsFalse(row.SizeKnown, "the unknown flag is what keeps 0 from being read as a measurement");
            Assert.AreEqual(0, row.ExpectedBytes);
            Assert.AreEqual(512, row.BytesWritten);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ADatabaseFromTheOldSchemaKeepsItsRowsAndGainsTheNewColumns()
    {
        string root = TempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".i-total"));
            string dbPath = Path.Combine(root, ".i-total", "session.db");
            // The exact schema shipped by the first 1.8.0 build, with one queued row.
            using (var old = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                old.Open();
                using var create = old.CreateCommand();
                create.CommandText = """
                    CREATE TABLE Sessions (SessionId TEXT PRIMARY KEY, DeviceName TEXT NOT NULL, Destination TEXT NOT NULL,
                        StartedUtc TEXT NOT NULL, UpdatedUtc TEXT NOT NULL, Status TEXT NOT NULL);
                    CREATE TABLE Files (Destination TEXT NOT NULL, RelativePath TEXT NOT NULL, ExpectedBytes INTEGER NOT NULL,
                        State TEXT NOT NULL, Attempts INTEGER NOT NULL, Error TEXT NULL, UpdatedUtc TEXT NOT NULL,
                        PRIMARY KEY (Destination, RelativePath));
                    CREATE TABLE Events (Id INTEGER PRIMARY KEY AUTOINCREMENT, CreatedUtc TEXT NOT NULL,
                        Level TEXT NOT NULL, Message TEXT NOT NULL);
                    INSERT INTO Files VALUES($dest,'DCIM\OLD.JPG',900,'Queued',0,NULL,'2026-10-20T00:00:00.0000000+00:00');
                    """;
                create.Parameters.AddWithValue("$dest", root);
                create.ExecuteNonQuery();
            }

            using (var database = new SessionDatabase(dbPath, new LogService(root)))
            {
                database.Initialize();
                PersistedFileState row = database.QueryFileStates(root).Single();
                Assert.AreEqual("DCIM\\OLD.JPG", row.RelativePath, "the migration must not lose the existing row");
                Assert.AreEqual(900, row.ExpectedBytes);
                Assert.IsTrue(row.SizeKnown, "the column default keeps an old row readable");
                Assert.AreEqual(0, row.BytesWritten);
                Assert.IsNull(row.SourceSha256);
                Assert.AreEqual(nameof(VerificationLevel.None), row.Verification);

                database.UpsertFile(root, Entry("DCIM\\OLD.JPG"));
                PersistedFileState updated = database.QueryFileStates(root).Single();
                Assert.AreEqual("CC33", updated.DestinationSha256);
                Assert.AreEqual(nameof(VerificationLevel.SourceAndDestinationHash), updated.Verification);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void TheSessionRowRecordsTheBackendAndEnumerationStatus()
    {
        string root = TempRoot();
        try
        {
            string dbPath = Path.Combine(root, ".i-total", "session.db");
            using var database = new SessionDatabase(dbPath, new LogService(root));
            database.Initialize();
            database.BeginSession("s1", "HUAWEI", root, "RUNNING", "Windows Portable Devices (native)", "Android", "Complete");
            database.EndSession("s1", "HASH-VERIFIED COMPLETE (source-to-destination)");

            SessionHistoryRow session = database.QueryRecentSessions().Single();
            Assert.AreEqual("HUAWEI", session.DeviceName);
            Assert.AreEqual("HASH-VERIFIED COMPLETE (source-to-destination)", session.Status);
        }
        finally { Directory.Delete(root, true); }
    }
}
