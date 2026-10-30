using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Exercises the wireless receiver over real loopback sockets. These are real network tests; if the
/// environment cannot bind a loopback port they are reported inconclusive rather than failing, because
/// that is an environment limitation, not a product defect.
///
/// <para>
/// Every security rule in the receiver is exercised here as a request, not as a comment: the pairing
/// secret is single-use, a secret is not a session token, uploads wait for approval, a refusal sticks,
/// traversal is rejected, and a declared digest that does not match is discarded.
/// </para>
/// </summary>
[TestClass]
public sealed class WirelessReceiverTests
{
    private static string TempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-wireless", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static AppSettings LoopbackSettings(int port, bool autoApprove)
    {
        AppSettings settings = AppSettings.Defaults();
        settings.WirelessBindAddress = "localhost";
        settings.WirelessPort = port;
        settings.WirelessRequireTls = false; // loopback HTTP keeps the test deterministic
        settings.PairingTokenMinutes = 5;
        settings.WirelessAutoApprove = autoApprove;
        settings.WirelessEnabled = true;
        return settings;
    }

    private static (WirelessReceiver receiver, string url) StartReceiver(string destination, bool autoApprove = false)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            int port = Random.Shared.Next(41000, 60000);
            var receiver = new WirelessReceiver(destination, LoopbackSettings(port, autoApprove), new LogService(destination));
            try
            {
                string url = receiver.Start();
                return (receiver, url);
            }
            catch (System.Net.Sockets.SocketException ex) { last = ex; receiver.Dispose(); }
        }
        Assert.Inconclusive("Could not bind a loopback TcpListener in this environment: " + last?.Message);
        throw new InvalidOperationException();
    }

    /// <summary>Redeems a fresh pairing secret and returns the session token, exactly as a sender would.</summary>
    private static string Pair(WirelessReceiver receiver, HttpClient client)
    {
        PairingTicket ticket = receiver.CreatePairingTicket();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/pair");
        request.Headers.Add("X-Pairing-Secret", ticket.Secret);
        HttpResponseMessage response = client.SendAsync(request).GetAwaiter().GetResult();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            "Pairing must succeed: " + response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        using JsonDocument json = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return json.RootElement.GetProperty("session").GetString()!;
    }

    private static HttpResponseMessage Upload(HttpClient client, string session, string path, byte[] payload, string? declaredHash = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/upload?path=" + Uri.EscapeDataString(path))
        {
            Content = new ByteArrayContent(payload)
        };
        request.Headers.Add("X-Session-Token", session);
        if (declaredHash is not null) request.Headers.Add("X-Content-Sha256", declaredHash);
        return client.SendAsync(request).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void UnauthorizedUploadIsRejectedAndFileIsNotWritten()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };

            HttpResponseMessage response = Upload(client, "", "DCIM/IMG_1.HEIC", [1, 2, 3]);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.IsFalse(File.Exists(Path.Combine(dir, "DCIM", "IMG_1.HEIC")));
            Assert.AreEqual(0, started.Value.receiver.Received.Count);
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void PairingSecretIsRedeemedOnceAndOnlyOnce()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            PairingTicket ticket = started.Value.receiver.CreatePairingTicket();

            using var first = new HttpRequestMessage(HttpMethod.Post, "/pair");
            first.Headers.Add("X-Pairing-Secret", ticket.Secret);
            Assert.AreEqual(HttpStatusCode.OK, client.SendAsync(first).GetAwaiter().GetResult().StatusCode);

            using var second = new HttpRequestMessage(HttpMethod.Post, "/pair");
            second.Headers.Add("X-Pairing-Secret", ticket.Secret);
            HttpResponseMessage response = client.SendAsync(second).GetAwaiter().GetResult();

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            StringAssert.Contains(response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), "already been used");
            Assert.AreEqual(1, started.Value.receiver.LiveSessions);
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void ThePairingSecretCannotBeUsedAsAnUploadCredential()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            PairingTicket ticket = started.Value.receiver.CreatePairingTicket();

            HttpResponseMessage response = Upload(client, ticket.Secret, "DCIM/IMG_1.HEIC", [1, 2, 3]);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode,
                "The displayed secret must not also be a write credential.");
            Assert.IsFalse(File.Exists(Path.Combine(dir, "DCIM", "IMG_1.HEIC")));
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void AnApprovedUploadStoresTheFileAndRecordsItsHash()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);
            byte[] payload = Encoding.UTF8.GetBytes("hello from the iphone");
            string expectedHash = Convert.ToHexString(SHA256.HashData(payload));

            Task<HttpResponseMessage> pending = Task.Run(() => Upload(client, session, "DCIM/IMG_1.HEIC", payload));
            Assert.IsTrue(SpinWait.SpinUntil(() => started.Value.receiver.Inbox.Pending.Count > 0, 5000),
                "The offer should appear in the approval list before anything is written.");
            Assert.IsFalse(File.Exists(Path.Combine(dir, "DCIM", "IMG_1.HEIC")));
            started.Value.receiver.Approve("DCIM/IMG_1.HEIC");
            HttpResponseMessage response = pending.GetAwaiter().GetResult();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            string stored = Path.Combine(dir, "DCIM", "IMG_1.HEIC");
            Assert.IsTrue(File.Exists(stored));
            CollectionAssert.AreEqual(payload, File.ReadAllBytes(stored));
            Assert.AreEqual(1, started.Value.receiver.Received.Count);
            Assert.AreEqual(expectedHash, started.Value.receiver.Received[0].Sha256, ignoreCase: true);
            Assert.IsFalse(string.IsNullOrWhiteSpace(started.Value.receiver.Received[0].Client),
                "The receipt must name the sender.");
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void AnUnapprovedUploadIsRefusedAndNothingReachesTheDestination()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);

            // No approval: the offer waits, then the refusal is delivered to the sender.
            Task<HttpResponseMessage> pending = Task.Run(() => Upload(client, session, "DCIM/IMG_2.HEIC", [9, 9, 9]));
            Assert.IsTrue(SpinWait.SpinUntil(() => started.Value.receiver.Inbox.Pending.Count > 0, 5000),
                "The offer should appear in the approval list.");

            started.Value.receiver.Refuse("DCIM/IMG_2.HEIC");
            HttpResponseMessage response = pending.GetAwaiter().GetResult();

            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.IsFalse(File.Exists(Path.Combine(dir, "DCIM", "IMG_2.HEIC")));
            Assert.AreEqual(0, started.Value.receiver.Received.Count);
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void AnOfferAppearsForTheOperatorBeforeAnyByteIsWritten()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);

            Task<HttpResponseMessage> pending = Task.Run(() => Upload(client, session, "DCIM/IMG_3.HEIC", [7, 7]));
            Assert.IsTrue(SpinWait.SpinUntil(() => started.Value.receiver.Inbox.Pending.Count > 0, 5000));

            OfferRecord offer = started.Value.receiver.Inbox.Pending[0];
            Assert.AreEqual("DCIM/IMG_3.HEIC", offer.Offer.RelativePath);
            Assert.IsFalse(File.Exists(Path.Combine(dir, "DCIM", "IMG_3.HEIC")),
                "Nothing may be written while the offer waits.");

            started.Value.receiver.Approve("DCIM/IMG_3.HEIC");
            Assert.AreEqual(HttpStatusCode.OK, pending.GetAwaiter().GetResult().StatusCode);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "DCIM", "IMG_3.HEIC")));
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void AutoApproveWritesWithoutTheApprovalList()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir, autoApprove: true);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);

            HttpResponseMessage response = Upload(client, session, "DCIM/IMG_4.HEIC", [4, 4, 4, 4]);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "DCIM", "IMG_4.HEIC")));
            Assert.AreEqual(0, started.Value.receiver.Inbox.Pending.Count);
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void ADigestThatDisagreesWithTheBytesIsDiscarded()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir, autoApprove: true);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);
            string wrong = new string('a', 64);

            HttpResponseMessage response = Upload(client, session, "DCIM/IMG_5.HEIC", [1, 2, 3], wrong);

            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.IsFalse(File.Exists(Path.Combine(dir, "DCIM", "IMG_5.HEIC")));
            Assert.AreEqual(0, started.Value.receiver.Received.Count);
            Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, ".i-total", "wireless")).Length,
                "A discarded payload must not leave a staging file behind.");
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void AMatchingDeclaredDigestIsAccepted()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir, autoApprove: true);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);
            byte[] payload = [5, 6, 7, 8, 9];

            HttpResponseMessage response = Upload(client, session, "DCIM/IMG_6.HEIC", payload,
                Convert.ToHexString(SHA256.HashData(payload)));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(payload, File.ReadAllBytes(Path.Combine(dir, "DCIM", "IMG_6.HEIC")));
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void TraversalPathIsRejectedEvenWhenAuthorized()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir, autoApprove: true);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);

            HttpResponseMessage response = Upload(client, session, "..\\..\\evil.txt", [1, 2, 3]);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(0, started.Value.receiver.Received.Count);
            Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(dir)!, "evil.txt")));
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void TheInternalSessionFolderCannotBeWrittenByASender()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir, autoApprove: true);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);

            HttpResponseMessage response = Upload(client, session, ".i-total/session.json", [1]);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.IsFalse(File.Exists(Path.Combine(dir, ".i-total", "session.json")));
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void AnExistingFileIsRenamedRatherThanOverwritten()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "DCIM"));
            File.WriteAllBytes(Path.Combine(dir, "DCIM", "IMG_7.HEIC"), [1, 1, 1]);
            started = StartReceiver(dir, autoApprove: true);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);

            HttpResponseMessage response = Upload(client, session, "DCIM/IMG_7.HEIC", [2, 2, 2, 2, 2]);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(new byte[] { 1, 1, 1 }, File.ReadAllBytes(Path.Combine(dir, "DCIM", "IMG_7.HEIC")),
                "The operator's existing file must not be overwritten.");
            Assert.AreNotEqual("DCIM/IMG_7.HEIC", started.Value.receiver.Received[0].RelativePath,
                "The receipt must record the name that was actually used.");
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void RotationIssuesAWorkingTokenAndRetiresTheOldOne()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir, autoApprove: true);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);

            using var rotate = new HttpRequestMessage(HttpMethod.Post, "/rotate");
            rotate.Headers.Add("X-Session-Token", session);
            HttpResponseMessage rotatedResponse = client.SendAsync(rotate).GetAwaiter().GetResult();
            Assert.AreEqual(HttpStatusCode.OK, rotatedResponse.StatusCode);
            using JsonDocument json = JsonDocument.Parse(rotatedResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            string rotated = json.RootElement.GetProperty("session").GetString()!;
            Assert.AreNotEqual(session, rotated);

            Assert.AreEqual(HttpStatusCode.Unauthorized,
                Upload(client, session, "DCIM/OLD.HEIC", [1]).StatusCode);
            Assert.AreEqual(HttpStatusCode.OK,
                Upload(client, rotated, "DCIM/NEW.HEIC", [2]).StatusCode);
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void StoppingRevokesTheSessionAndTheTicket()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir, autoApprove: true);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);
            Assert.AreEqual(1, started.Value.receiver.LiveSessions);

            started.Value.receiver.Stop();

            Assert.IsFalse(started.Value.receiver.IsRunning);
            Assert.AreEqual(0, started.Value.receiver.LiveSessions);
            Assert.AreEqual(0, started.Value.receiver.Inbox.Pending.Count);
            Assert.IsNull(started.Value.receiver.TicketExpiresUtc);
            Assert.IsFalse(string.IsNullOrEmpty(session));
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void HealthEndpointNeedsNoTokenAndDoesNotLeakTheSecret()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir);
            PairingTicket ticket = started.Value.receiver.CreatePairingTicket();
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };

            HttpResponseMessage response = client.GetAsync("/health").GetAwaiter().GetResult();
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsFalse(body.Contains(ticket.Secret), "The health endpoint must never echo the pairing secret.");
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void APrivateLanAddressBindsWithoutAUrlAcl()
    {
        // This is the regression for the audit finding: HTTP.sys refused a LAN prefix for a non-elevated
        // user, so the receiver now owns a TcpListener instead. A real LAN address is used on purpose;
        // loopback would not exercise the permission that used to fail.
        string? lan = WirelessReceiver.AvailableAddresses()
            .FirstOrDefault(address => address != "127.0.0.1" &&
                                       PrivateNetwork.IsPrivate(IPAddress.Parse(address)));
        if (lan is null)
            Assert.Inconclusive("No private LAN IPv4 address is assigned to this machine.");

        string dir = TempDir();
        WirelessReceiver? receiver = null;
        try
        {
            Exception? last = null;
            for (int attempt = 0; attempt < 6 && receiver is null; attempt++)
            {
                int port = Random.Shared.Next(41000, 60000);
                AppSettings settings = LoopbackSettings(port, autoApprove: false);
                settings.WirelessBindAddress = lan;
                var candidate = new WirelessReceiver(dir, settings, new LogService(dir));
                try
                {
                    Assert.AreEqual($"http://{lan}:{port}/", candidate.Start());
                    receiver = candidate;
                }
                catch (System.Net.Sockets.SocketException ex) { last = ex; candidate.Dispose(); }
            }
            if (receiver is null)
                Assert.Inconclusive("Could not bind a private LAN port in this environment: " + last?.Message);

            using var client = new HttpClient { BaseAddress = new Uri(receiver.BaseUrl) };
            HttpResponseMessage health = client.GetAsync("/health").GetAwaiter().GetResult();
            string body = health.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            Assert.AreEqual(HttpStatusCode.OK, health.StatusCode, body);
            StringAssert.Contains(body, "\"status\":\"ok\"");
            Assert.IsTrue(receiver.IsRunning);
        }
        finally
        {
            receiver?.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void AReceiverRefusesToBindAPublicAddress()
    {
        string dir = TempDir();
        try
        {
            AppSettings settings = LoopbackSettings(47821, autoApprove: false);
            settings.WirelessBindAddress = "203.0.113.9";
            using var receiver = new WirelessReceiver(dir, settings, new LogService(dir));

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() => receiver.Start());

            StringAssert.Contains(ex.Message, "not a private address");
            Assert.IsFalse(receiver.IsRunning);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void ASecondUploadOfAPathAlreadyAwaitingApprovalIsToldSo()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);

            Task<HttpResponseMessage> first = Task.Run(() => Upload(client, session, "DCIM/IMG_9.HEIC", [1, 1, 1]));
            Assert.IsTrue(SpinWait.SpinUntil(() => started.Value.receiver.Inbox.Pending.Count > 0, 5000));

            // The same path while the first is still waiting: a second waiter would replace the first
            // and leave it hanging, so it is refused outright.
            HttpResponseMessage second = Upload(client, session, "DCIM/IMG_9.HEIC", [2, 2]);
            Assert.AreEqual(HttpStatusCode.Conflict, second.StatusCode);

            started.Value.receiver.Refuse("DCIM/IMG_9.HEIC");
            Assert.AreEqual(HttpStatusCode.Forbidden, first.GetAwaiter().GetResult().StatusCode);
            Assert.IsFalse(File.Exists(Path.Combine(dir, "DCIM", "IMG_9.HEIC")));
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void StartingSweepsStagingFilesLeftByACrash()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            string staging = Path.Combine(dir, ".i-total", "wireless");
            Directory.CreateDirectory(staging);
            string orphan = Path.Combine(staging, Guid.NewGuid().ToString("N") + ".partial");
            File.WriteAllBytes(orphan, [1, 2, 3]);

            started = StartReceiver(dir);

            Assert.IsFalse(File.Exists(orphan), "An unapproved fragment from a previous run must be removed.");
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void AReceiverRefusesToStartUnlessWirelessIsEnabled()
    {
        string dir = TempDir();
        try
        {
            AppSettings settings = LoopbackSettings(47821, autoApprove: false);
            settings.WirelessEnabled = false;
            using var receiver = new WirelessReceiver(dir, settings, new LogService(dir));

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() => receiver.Start());

            StringAssert.Contains(ex.Message, "switched off");
            Assert.IsFalse(receiver.IsRunning);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void AReceiverRefusesTheWildcard()
    {
        string dir = TempDir();
        try
        {
            AppSettings settings = LoopbackSettings(47821, autoApprove: false);
            settings.WirelessBindAddress = "0.0.0.0";
            using var receiver = new WirelessReceiver(dir, settings, new LogService(dir));

            Assert.ThrowsException<InvalidOperationException>(() => receiver.Start());
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public async Task AClientThatDisconnectsMidUploadLeavesNoPartialOrDestination()
    {
        string dir = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(dir, autoApprove: true);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };
            string session = Pair(started.Value.receiver, client);
            Uri baseUri = client.BaseAddress!;

            using (var raw = new TcpClient())
            {
                await raw.ConnectAsync(baseUri.Host, baseUri.Port);
                using NetworkStream stream = raw.GetStream();
                string head = "POST /upload?path=DCIM/IMG_DROP.HEIC HTTP/1.1\r\n" +
                              $"Host: {baseUri.Host}\r\n" +
                              $"X-Session-Token: {session}\r\n" +
                              "Content-Type: application/octet-stream\r\n" +
                              "Content-Length: 1048576\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
                await stream.WriteAsync(new byte[8192]);
                await stream.FlushAsync();
            }

            await Task.Delay(1500);

            Assert.IsFalse(File.Exists(Path.Combine(dir, "DCIM", "IMG_DROP.HEIC")),
                "an interrupted upload must never become a destination file");
            Assert.AreEqual(0, started.Value.receiver.Received.Count);
            string staging = Path.Combine(dir, ".i-total", "wireless");
            if (Directory.Exists(staging))
                Assert.AreEqual(0, Directory.GetFiles(staging, "*.partial").Length,
                    "an interrupted upload must not leave a partial staging file");
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(dir, true);
        }
    }
}
