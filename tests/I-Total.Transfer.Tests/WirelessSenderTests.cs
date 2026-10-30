using System.Net;
using System.Net.Http;
using System.Text.Json;
using ITotalTransfer.Core;
using ITotalTransfer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The receiver serves a real browser sender at <c>/</c>, <c>/send</c> and the ticket URL. These tests
/// exercise that page over loopback sockets: it must be reachable without a credential, it must not spend
/// the single-use pairing secret merely by being opened, and the API's JSON pairing path must keep working.
/// They do not run a browser; the page's SHA-256 and path logic is covered by <c>tests/sender/sender.test.mjs</c>.
/// </summary>
[TestClass]
public sealed class WirelessSenderTests
{
    private static string TempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "itotal-sender", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static AppSettings LoopbackSettings(int port)
    {
        AppSettings settings = AppSettings.Defaults();
        settings.WirelessBindAddress = "localhost";
        settings.WirelessPort = port;
        settings.WirelessRequireTls = false;
        settings.PairingTokenMinutes = 5;
        settings.WirelessAutoApprove = true;
        settings.WirelessEnabled = true;
        return settings;
    }

    private static (WirelessReceiver receiver, string url) StartReceiver(string destination)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            int port = Random.Shared.Next(41000, 60000);
            var receiver = new WirelessReceiver(destination, LoopbackSettings(port), new LogService(destination));
            try { return (receiver, receiver.Start()); }
            catch (System.Net.Sockets.SocketException ex) { last = ex; receiver.Dispose(); }
        }
        Assert.Inconclusive("Could not bind a loopback TcpListener in this environment: " + last?.Message);
        throw new InvalidOperationException();
    }

    private static async Task<(HttpStatusCode Status, string? ContentType, string Body, string? Csp)> GetAsync(
        HttpClient client, string path, string? accept = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (accept is not null) request.Headers.TryAddWithoutValidation("Accept", accept);
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        string? csp = response.Headers.TryGetValues("Content-Security-Policy", out var values)
            ? string.Join(" ", values)
            : null;
        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, body, csp);
    }

    [TestMethod]
    public async Task TheSenderPageIsServedWithoutASecret()
    {
        string destination = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(destination);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };

            var page = await GetAsync(client, "/", "text/html");

            Assert.AreEqual(HttpStatusCode.OK, page.Status);
            Assert.AreEqual("text/html", page.ContentType);
            StringAssert.Contains(page.Body, "id=\"sender\"");
            StringAssert.Contains(page.Body, "id=\"secret\"");
            StringAssert.Contains(page.Body, "Only the files you choose are sent");
            StringAssert.Contains(page.Body, "/sender.js");
            Assert.IsFalse(page.Body.Contains("X-Session-Token", StringComparison.Ordinal),
                "the page itself must not carry a session token");
            Assert.IsNotNull(page.Csp);
            StringAssert.Contains(page.Csp, "default-src 'none'");
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(destination, true);
        }
    }

    [TestMethod]
    public async Task TheSenderScriptIsServedAndCarriesTheDigestAndPathRules()
    {
        string destination = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(destination);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };

            var script = await GetAsync(client, "/sender.js");

            Assert.AreEqual(HttpStatusCode.OK, script.Status);
            Assert.AreEqual("application/javascript", script.ContentType);
            StringAssert.Contains(script.Body, "sha256Hex");
            StringAssert.Contains(script.Body, "normalizePath");
            StringAssert.Contains(script.Body, "class Sha256");
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(destination, true);
        }
    }

    [TestMethod]
    public async Task OpeningTheTicketUrlInABrowserDoesNotSpendTheSecret()
    {
        string destination = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(destination);
            PairingTicket ticket = started.Value.receiver.CreatePairingTicket();
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };

            // A browser never sends the fragment. Opening the ticket URL is therefore a plain GET of
            // /pair, which must return the page without redeeming the secret.
            var page = await GetAsync(client, "pair", "text/html,application/xhtml+xml");
            Assert.AreEqual(HttpStatusCode.OK, page.Status);
            Assert.AreEqual("text/html", page.ContentType);
            Assert.IsFalse(started.Value.receiver.TicketWasUsed, "merely opening the ticket URL must not spend the secret");

            using var pair = new HttpRequestMessage(HttpMethod.Post, "pair");
            pair.Headers.Add("X-Pairing-Secret", ticket.Secret);
            using HttpResponseMessage paired = await client.SendAsync(pair);
            Assert.AreEqual(HttpStatusCode.OK, paired.StatusCode);
            Assert.IsTrue(started.Value.receiver.TicketWasUsed);
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(destination, true);
        }
    }

    [TestMethod]
    public async Task TheTicketSecretTravelsInTheFragmentAndNeverInARequestTarget()
    {
        string destination = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(destination);
            PairingTicket ticket = started.Value.receiver.CreatePairingTicket();
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };

            var uri = new Uri(ticket.Url);
            Assert.AreEqual("#token=" + ticket.Secret, uri.Fragment, "the ticket must carry the secret in the fragment");
            Assert.AreEqual("", uri.Query, "the ticket must not put the secret in the query string");
            Assert.IsFalse(uri.AbsolutePath.Contains(ticket.Secret, StringComparison.Ordinal),
                "the request path must not contain the secret");
            Assert.IsFalse(ticket.Qr.Modules.Length == 0);
            Assert.AreEqual(ticket.Url, ticket.Qr.Text, "the QR must encode the same fragment URL");

            // The actual request target the browser sends: path only, no query, no fragment.
            string target = uri.AbsolutePath;
            Assert.IsFalse(target.Contains(ticket.Secret, StringComparison.Ordinal));

            var page = await GetAsync(client, target, "text/html");
            Assert.AreEqual(HttpStatusCode.OK, page.Status);
            Assert.IsFalse(page.Body.Contains(ticket.Secret, StringComparison.Ordinal),
                "the served page must not echo the pairing secret");

            var script = await GetAsync(client, "/sender.js");
            Assert.AreEqual(HttpStatusCode.OK, script.Status);
            Assert.IsFalse(script.Body.Contains(ticket.Secret, StringComparison.Ordinal),
                "an asset request must not carry the pairing secret");
            Assert.IsFalse(started.Value.receiver.TicketWasUsed);
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(destination, true);
        }
    }

    [TestMethod]
    public async Task AnApiClientStillRedeemsTheTicketWithAJsonGet()
    {
        string destination = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(destination);
            PairingTicket ticket = started.Value.receiver.CreatePairingTicket();
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };

            var response = await GetAsync(client, $"pair?token={Uri.EscapeDataString(ticket.Secret)}", "application/json");

            Assert.AreEqual(HttpStatusCode.OK, response.Status);
            Assert.AreEqual("application/json", response.ContentType);
            using JsonDocument json = JsonDocument.Parse(response.Body);
            Assert.AreEqual("paired", json.RootElement.GetProperty("status").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("session").GetString()));
            Assert.IsTrue(started.Value.receiver.TicketWasUsed);
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(destination, true);
        }
    }

    [TestMethod]
    public async Task TheSendAliasServesTheSamePage()
    {
        string destination = TempDir();
        (WirelessReceiver receiver, string url)? started = null;
        try
        {
            started = StartReceiver(destination);
            using var client = new HttpClient { BaseAddress = new Uri(started.Value.url) };

            var page = await GetAsync(client, "/send", "text/html");
            Assert.AreEqual(HttpStatusCode.OK, page.Status);
            StringAssert.Contains(page.Body, "id=\"sender\"");
        }
        finally
        {
            started?.receiver.Dispose();
            Directory.Delete(destination, true);
        }
    }
}
