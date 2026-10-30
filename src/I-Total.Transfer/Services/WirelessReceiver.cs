using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ITotalTransfer.Core;
using ITotalTransfer.Licensing;

namespace ITotalTransfer.Services;

/// <summary>
/// A pairing ticket: the single-use secret shown to the operator (as a URL and a QR code) and the
/// moment it stops being usable. The secret is deliberately not the session token — redeeming it once
/// mints a session token, and only that token may upload.
/// </summary>
public sealed record PairingTicket(string Secret, DateTimeOffset ExpiresUtc, string Url, QrCode.Symbol Qr)
{
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresUtc;
}

public sealed record WirelessEvent(string Kind, string Message, bool Success);

public sealed record ReceivedFile(
    string RelativePath,
    long Bytes,
    string Sha256,
    DateTimeOffset ReceivedUtc,
    string Client = "");

/// <summary>
/// Authenticated LAN receiver for iPhone-originated uploads.
///
/// <para>
/// A receiver alone cannot enumerate an iPhone Photos library: an explicit iOS sender (a Shortcut or a
/// companion app) must select assets and POST them. The security model is the one the plan asks for,
/// and each rule is enforced here rather than described in a comment:
/// </para>
/// <list type="bullet">
/// <item><b>Private interface only.</b> The bind address is validated by <see cref="PrivateNetwork"/>; a
/// wildcard or public address is refused outright instead of exposing the receiver to whatever network
/// the machine later joins.</item>
/// <item><b>A single-use pairing secret.</b> Shown once as a URL and a QR code, redeemable exactly once,
/// and expiring on a short clock (<see cref="PairingState"/>).</item>
/// <item><b>Rotating session tokens.</b> Redeeming the secret mints a bounded-lifetime session token;
/// the sender can rotate it before a long transfer. Tokens are revoked when the receiver stops.</item>
/// <item><b>Operator approval of incoming files.</b> A POST does not write to the destination. It is held
/// as an offer until the operator approves it in the Wireless page (<see cref="ApprovalInbox"/>), unless
/// the operator has explicitly switched automatic approval on.</item>
/// <item><b>Strict paths and bounded writes.</b> Relative-path validation, reserved-name rejection, a
/// per-file cap, and a free-space check before a byte is written.</item>
/// <item><b>Hash on receipt.</b> Every payload is hashed while streaming; a sender-declared digest that
/// disagrees is rejected and the staging file is discarded.</item>
/// </list>
/// </summary>
public sealed class WirelessReceiver : IDisposable
{
    private const long DefaultMaxFileBytes = 8L * 1024 * 1024 * 1024;
    private static readonly string[] ReservedPaths = [".i-total", "I-total-transfer.log"];

    private readonly string _destination;
    private readonly LogService _log;
    private readonly LicenseService? _license;
    private readonly int _port;
    private readonly string _bindAddress;
    private readonly bool _requireTls;
    private readonly bool _enabled;
    private readonly int _tokenMinutes;
    private readonly object _sync = new();
    private readonly object _commitSync = new();

    private TcpListener? _listener;
    private X509Certificate2? _serverCertificate;
    private CancellationTokenSource? _cts;
    private readonly PairingState _pairing = new();
    private readonly ApprovalInbox _inbox = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _waiting = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ReceivedFile> _received = [];
    private PairingTicket? _ticket;

    /// <summary>How long a POST waits for the operator's decision before it is refused.</summary>
    public TimeSpan ApprovalTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public bool IsRunning { get; private set; }
    public string BaseUrl { get; private set; } = "";
    public bool UsingTls { get; private set; }

    /// <summary>Why the receiver is not using TLS, when it is not.</summary>
    public string TlsNote { get; private set; } = "";

    public IReadOnlyList<ReceivedFile> Received { get { lock (_sync) return _received.ToArray(); } }

    /// <summary>The approval gate. The UI reads it and decides; the network never can.</summary>
    public ApprovalInbox Inbox => _inbox;

    /// <summary>When the outstanding pairing secret expires, if one exists.</summary>
    public DateTimeOffset? TicketExpiresUtc => _ticket?.ExpiresUtc;

    /// <summary>True when the outstanding secret has already been redeemed.</summary>
    public bool TicketWasUsed => _pairing.SecretWasUsed;

    /// <summary>Number of live sender sessions.</summary>
    public int LiveSessions => _pairing.LiveSessionCount(DateTimeOffset.UtcNow);

    public event Action<WirelessEvent>? Activity;

    /// <summary>Raised when an offer needs a decision, so the UI can show it immediately.</summary>
    public event Action<OfferRecord>? OfferArrived;

    public WirelessReceiver(string destination, AppSettings settings, LogService log, LicenseService? license = null)
    {
        _destination = Path.GetFullPath(destination);
        _log = log;
        _license = license;
        _port = settings.WirelessPort;
        _requireTls = settings.WirelessRequireTls;
        _enabled = settings.WirelessEnabled;
        _tokenMinutes = settings.PairingTokenMinutes;
        _inbox.AutoApprove = settings.WirelessAutoApprove;
        _bindAddress = string.IsNullOrWhiteSpace(settings.WirelessBindAddress)
            ? SelectPrivateAddress()
            : NormalizeBindAddress(settings.WirelessBindAddress);
    }

    /// <summary>
    /// A <see cref="TcpListener"/> needs an address, not a host name. "localhost" stays a supported
    /// spelling for the loopback tests and the UI, but it is resolved here so the socket and the
    /// displayed URL name the same endpoint.
    /// </summary>
    private static string NormalizeBindAddress(string address)
    {
        string value = address.Trim();
        return value.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : value;
    }

    /// <summary>Enumerates usable private IPv4 addresses (excludes loopback/APIPA).</summary>
    public static IReadOnlyList<string> AvailableAddresses()
    {
        var result = new List<string> { "127.0.0.1" };
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
            {
                if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                string ip = info.Address.ToString();
                if (ip.StartsWith("169.254.")) continue;
                result.Add(ip);
            }
        }
        return result.Distinct().ToList();
    }

    private static string SelectPrivateAddress()
    {
        foreach (string ip in AvailableAddresses())
        {
            if (ip == "127.0.0.1") continue;
            if (PrivateNetwork.IsPrivate(IPAddress.Parse(ip))) return ip;
        }
        return "127.0.0.1";
    }

    /// <summary>
    /// Creates the pairing secret and the URL and QR that carry it. The secret is single-use; the QR is
    /// the same URL, so scanning it and typing it are the same act.
    /// </summary>
    public PairingTicket CreatePairingTicket()
    {
        if (!IsRunning) throw new InvalidOperationException("Start the receiver before creating a pairing ticket.");
        if (!PrivateNetwork.IsAllowedBindAddress(_bindAddress))
            throw new InvalidOperationException(PrivateNetwork.RejectionReason(_bindAddress) ?? "The bind address is not private.");

        TimeSpan lifetime = TimeSpan.FromMinutes(_tokenMinutes);
        string secret = _pairing.CreateSecret(DateTimeOffset.UtcNow, lifetime);
        // The secret travels in the fragment, which a browser never sends in the request target. The
        // page reads it locally and immediately removes it from the address bar. The JSON API still
        // accepts the query form (?token=) on /pair; new tickets never use it.
        string url = $"{BaseUrl}pair#token={secret}";
        var ticket = new PairingTicket(secret, DateTimeOffset.UtcNow.Add(lifetime), url, QrCode.Encode(url));
        lock (_sync) _ticket = ticket;

        _log.Write("Wireless pairing ticket created (value redacted); single use, expires in " + _tokenMinutes + " minutes.");
        Raise("ticket", "Pairing ticket created: show the QR code to the sender. It can be used once.", true);
        return ticket;
    }

    public string Start()
    {
        if (IsRunning) return BaseUrl;
        // Wireless is an opt-in: the receiver does not listen because a port happens to be free.
        if (!_enabled)
            throw new InvalidOperationException("Wireless receiving is switched off. Enable it on the Wireless page first.");
        if (!PrivateNetwork.IsAllowedBindAddress(_bindAddress))
            throw new InvalidOperationException(PrivateNetwork.RejectionReason(_bindAddress) ?? "The bind address is not private.");

        string stagingRoot = Path.Combine(_destination, ".i-total", "wireless");
        Directory.CreateDirectory(stagingRoot);
        SweepOrphanedStaging(stagingRoot);

        // HttpListener delegates to HTTP.sys, which requires an HTTP URL ACL for every non-loopback
        // prefix. A normal per-user installation cannot add that ACL without elevation, so the
        // receiver uses a plain TcpListener and speaks HTTP/1.1 itself. This keeps a private-interface
        // bind available to the non-elevated user the installer is designed for.
        IPAddress address = IPAddress.Parse(_bindAddress);
        X509Certificate2? certificate = _requireTls ? FindServerCertificate(_bindAddress) : null;
        // "Require TLS" is a hard requirement, not a preference. Silently downgrading to HTTP would
        // make the setting lie about the transport, so the receiver refuses to start instead.
        if (_requireTls && certificate is null)
        {
            _log.Write("Wireless was not started: TLS is required but no usable certificate names the bind address.", "ERROR");
            throw new InvalidOperationException(
                $"Require TLS is switched on, but no certificate in CurrentUser\\My has a private key, is " +
                $"currently valid and names {_bindAddress}. Install a matching certificate, or clear " +
                "\"Require TLS\" on the Wireless page to run the token-authenticated session over the " +
                "private network without encryption. The receiver did not start.");
        }
        bool useTls = certificate is not null;
        var listener = new TcpListener(address, _port);
        listener.Start();

        _listener = listener;
        _serverCertificate = certificate;
        UsingTls = useTls;
        TlsNote = useTls
            ? "TLS is active with the certificate found in CurrentUser\\My."
            : "TLS is switched off in Settings; the token-authenticated session runs over the private network without encryption.";

        _cts = new CancellationTokenSource();
        IsRunning = true;
        // The URL must describe the listener that actually started, not the scheme that was requested.
        BaseUrl = $"{(UsingTls ? "https" : "http")}://{_bindAddress}:{_port}/";
        _ = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));
        _log.Write($"Wireless receiver listening on {BaseUrl} (TLS={UsingTls}).");
        Raise("start", $"Listening on {BaseUrl}{(UsingTls ? "" : " (private-network HTTP; TLS is switched off, so traffic is not encrypted)")}", true);
        return BaseUrl;
    }

    /// <summary>
    /// Finds a certificate usable for this bind address without requiring an elevated HTTP.sys
    /// reservation. A certificate is accepted only when it has a private key, is currently valid and
    /// its subject or subject alternative names include the address being served.
    /// </summary>
    private static X509Certificate2? FindServerCertificate(string address)
    {
        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            DateTime now = DateTime.Now;
            foreach (X509Certificate2 candidate in store.Certificates)
            {
                if (!candidate.HasPrivateKey || now < candidate.NotBefore || now > candidate.NotAfter) continue;
                if (!CertificateNamesAddress(candidate, address)) continue;
                return new X509Certificate2(candidate);
            }
        }
        catch (CryptographicException) { }
        catch (PlatformNotSupportedException) { }
        return null;
    }

    private static bool CertificateNamesAddress(X509Certificate2 certificate, string address)
    {
        if (certificate.Subject.Contains(address, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (X509Extension extension in certificate.Extensions)
        {
            if (extension.Oid?.Value != "2.5.29.17") continue;
            try
            {
                if (extension.Format(false).Contains(address, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (CryptographicException) { }
        }
        return false;
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _listener = null;
        try { _serverCertificate?.Dispose(); } catch { }
        _serverCertificate = null;
        IsRunning = false;
        // A stopped receiver has no sessions, no secret and no pending offers.
        _pairing.RevokeAll();
        _inbox.Clear();
        foreach (TaskCompletionSource<bool> wait in _waiting.Values) wait.TrySetResult(false);
        _waiting.Clear();
        lock (_sync) _ticket = null;
        _log.Write("Wireless receiver stopped; pairing secret and sessions revoked.");
        Raise("stop", "Wireless receiver stopped.", true);
    }

    // ------------------------------------------------------------------ operator decisions

    /// <summary>Approves one offer. Only this process can do it; there is no network route to approval.</summary>
    public bool Approve(string relativePath)
    {
        OfferRecord? record = _inbox.Approve(relativePath, DateTimeOffset.UtcNow);
        if (record is null) return false;
        if (_waiting.TryRemove(record.Offer.RelativePath, out TaskCompletionSource<bool>? wait))
            wait.TrySetResult(record.State == OfferState.Approved);
        Raise("approve", $"Approved {record.Offer.RelativePath}.", true);
        return true;
    }

    /// <summary>Refuses one offer; the path stays refused for the rest of the session.</summary>
    public bool Refuse(string relativePath)
    {
        OfferRecord? record = _inbox.Refuse(relativePath, DateTimeOffset.UtcNow);
        if (record is null) return false;
        if (_waiting.TryRemove(record.Offer.RelativePath, out TaskCompletionSource<bool>? wait))
            wait.TrySetResult(false);
        Raise("refuse", $"Refused {record.Offer.RelativePath}.", false);
        return true;
    }

    /// <summary>Approves every waiting offer.</summary>
    public int ApproveAll()
    {
        int changed = _inbox.ApproveAll(DateTimeOffset.UtcNow);
        foreach (string path in _waiting.Keys.ToArray())
            if (_inbox.IsApproved(path) && _waiting.TryRemove(path, out TaskCompletionSource<bool>? wait))
                wait.TrySetResult(true);
        if (changed > 0) Raise("approve", $"Approved {changed} waiting file(s).", true);
        return changed;
    }

    /// <summary>Refuses every waiting offer.</summary>
    public int RefuseAll()
    {
        int changed = _inbox.RefuseAll(DateTimeOffset.UtcNow);
        foreach (string path in _waiting.Keys.ToArray())
            if (_waiting.TryRemove(path, out TaskCompletionSource<bool>? wait))
                wait.TrySetResult(false);
        if (changed > 0) Raise("refuse", $"Refused {changed} waiting file(s).", false);
        return changed;
    }

    // ------------------------------------------------------------------ HTTP

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { if (token.IsCancellationRequested) break; continue; }
            _ = Task.Run(() => ServeConnectionAsync(client, token), CancellationToken.None);
        }
    }

    private async Task ServeConnectionAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            client.NoDelay = true;
            Stream stream = client.GetStream();
            SslStream? tls = null;
            try
            {
                if (UsingTls && _serverCertificate is not null)
                {
                    tls = new SslStream(stream, leaveInnerStreamOpen: false);
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _serverCertificate,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.None,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                    }, token).ConfigureAwait(false);
                    stream = tls;
                }

                WirelessRequest? request = await ReadRequestAsync(stream, client, token).ConfigureAwait(false);
                if (request is not null) await HandleAsync(request, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (AuthenticationException ex) { _log.Write("Wireless TLS handshake failed: " + ex.Message, "WARN"); }
            catch (Exception ex) { _log.Write("Wireless connection failed: " + ex.Message, "WARN"); }
            finally
            {
                if (tls is not null)
                {
                    try { await tls.DisposeAsync().ConfigureAwait(false); } catch { }
                }
            }
        }
    }

    /// <summary>
    /// Reads the request line and headers, leaving the body in the same buffered stream so the upload
    /// path can hash it while writing. Only Content-Length bodies are accepted: the protocol is a
    /// sender-to-PC upload where the size is needed for the free-space check and the size check.
    /// </summary>
    private async Task<WirelessRequest?> ReadRequestAsync(Stream stream, TcpClient client, CancellationToken token)
    {
        var buffered = new BufferedStream(stream, 64 * 1024);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        var header = new List<byte>(1024);
        var one = new byte[1];
        while (header.Count < 64 * 1024)
        {
            int read = await buffered.ReadAsync(one.AsMemory(0, 1), timeout.Token).ConfigureAwait(false);
            if (read == 0) return null;
            header.Add(one[0]);
            int n = header.Count;
            if (n >= 4 && header[n - 1] == (byte)'\n' && header[n - 2] == (byte)'\r' &&
                header[n - 3] == (byte)'\n' && header[n - 4] == (byte)'\r')
                break;
        }
        if (header.Count == 0) return null;

        string[] lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0])) return null;
        string[] requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestLine.Length < 2) return null;

        string method = requestLine[0].ToUpperInvariant();
        string target = requestLine[1];
        string path;
        string query;
        if (Uri.TryCreate(target, UriKind.Absolute, out Uri? absolute))
        {
            path = absolute.AbsolutePath;
            query = absolute.Query;
        }
        else
        {
            int q = target.IndexOf('?');
            path = q < 0 ? target : target[..q];
            query = q < 0 ? "" : target[q..];
        }
        if (string.IsNullOrEmpty(path)) path = "/";

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0) break;
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            string name = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();
            headers[name] = value;
        }

        long contentLength = 0;
        if (headers.TryGetValue("Content-Length", out string? declaredLength))
        {
            if (!long.TryParse(declaredLength, out contentLength) || contentLength < 0) return null;
        }
        else if (headers.TryGetValue("Transfer-Encoding", out string? transferEncoding) &&
                 transferEncoding.Contains("chunked", StringComparison.OrdinalIgnoreCase))
        {
            contentLength = -1;
        }

        if (headers.TryGetValue("Expect", out string? expect) &&
            expect.Contains("100-continue", StringComparison.OrdinalIgnoreCase))
        {
            byte[] continueBytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
            await buffered.WriteAsync(continueBytes, timeout.Token).ConfigureAwait(false);
            await buffered.FlushAsync(timeout.Token).ConfigureAwait(false);
        }

        string remote = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "unknown";
        return new WirelessRequest(
            buffered, method, path, ParseQuery(query), headers, contentLength, remote,
            headers.TryGetValue("User-Agent", out string? agent) ? agent : "");
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            string name = equals < 0 ? pair : pair[..equals];
            string value = equals < 0 ? "" : pair[(equals + 1)..];
            try
            {
                result[Uri.UnescapeDataString(name.Replace('+', ' '))] =
                    Uri.UnescapeDataString(value.Replace('+', ' '));
            }
            catch (UriFormatException) { }
        }
        return result;
    }

    private async Task HandleAsync(WirelessRequest request, CancellationToken token)
    {
        try
        {
            string path = request.Path;

            // The browser sender. It carries no secret of its own: the single-use pairing code is either
            // typed in or arrives in the ticket URL, and pairing is a deliberate button press.
            if (request.Method == "GET" && path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
            {
                await WriteNoContentAsync(request).ConfigureAwait(false);
                return;
            }
            if (request.Method == "GET" && path.Equals("/sender.js", StringComparison.OrdinalIgnoreCase))
            {
                await WriteAssetAsync(request, "application/javascript; charset=utf-8", WirelessSenderPage.JavaScript).ConfigureAwait(false);
                return;
            }
            if (request.Method == "GET" &&
                (path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
                 path.Equals("/send", StringComparison.OrdinalIgnoreCase) ||
                 (path.Equals("/pair", StringComparison.OrdinalIgnoreCase) && WantsHtml(request))))
            {
                await WriteAssetAsync(request, "text/html; charset=utf-8", WirelessSenderPage.Html).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(request, 200, new { status = "ok", tls = UsingTls }).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/pair", StringComparison.OrdinalIgnoreCase))
            {
                await HandlePairAsync(request).ConfigureAwait(false);
                return;
            }

            // Everything below requires a live session token.
            string? credential = request.Header("X-Session-Token")
                                 ?? request.Header("X-Pairing-Token");
            PairingDecision decision = _pairing.Authorize(credential, ClientOf(request), DateTimeOffset.UtcNow);
            if (!decision.IsAccepted)
            {
                _log.Write($"Wireless request from {ClientOf(request)} rejected: {decision.Outcome} - {decision.Detail}", "WARN");
                await DrainBodyAsync(request, token).ConfigureAwait(false);
                await WriteJsonAsync(request, 401, new { error = "unauthorized", detail = decision.Detail }).ConfigureAwait(false);
                Raise("reject", "Rejected a request: " + decision.Detail, false);
                return;
            }

            if (path.Equals("/rotate", StringComparison.OrdinalIgnoreCase))
            {
                string? rotated = _pairing.Rotate(credential!, DateTimeOffset.UtcNow);
                if (rotated is null) { await WriteJsonAsync(request, 401, new { error = "unauthorized" }).ConfigureAwait(false); return; }
                _log.Write("Wireless session token rotated at the sender's request.");
                await WriteJsonAsync(request, 200, new { session = rotated, expiresUtc = DateTimeOffset.UtcNow.Add(_pairing.SessionLifetime) }).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/upload", StringComparison.OrdinalIgnoreCase) && request.Method == "POST")
            {
                await HandleUploadAsync(request, credential!, ClientOf(request), token).ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(request, 404, new { error = "not found" }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Write($"Wireless request failed: {ex.Message}", "ERROR");
            try { await WriteJsonAsync(request, 500, new { error = ex.Message }).ConfigureAwait(false); } catch { }
        }
    }

    /// <summary>Redeems a single-use pairing secret for a session token. The secret is never accepted again.</summary>
    private async Task HandlePairAsync(WirelessRequest request)
    {
        string? secret = (request.Query.TryGetValue("token", out string? fromQuery) ? fromQuery : null)
                         ?? request.Header("X-Pairing-Secret");
        PairingDecision decision = _pairing.Redeem(secret, ClientOf(request), DateTimeOffset.UtcNow);

        if (decision.Outcome == PairingOutcome.Paired)
        {
            _log.Write($"Wireless pairing secret redeemed by {ClientOf(request)}; a session token was issued (value redacted).");
            Raise("paired", $"Paired with {ClientOf(request)}. The pairing secret is now spent.", true);
            await WriteJsonAsync(request, 200, new
            {
                status = "paired",
                session = decision.SessionToken,
                expiresUtc = DateTimeOffset.UtcNow.Add(_pairing.SessionLifetime),
                tls = UsingTls
            }).ConfigureAwait(false);
            return;
        }

        _log.Write($"Wireless pairing attempt from {ClientOf(request)} refused: {decision.Outcome} - {decision.Detail}", "WARN");
        Raise("reject", "Pairing refused: " + decision.Detail, false);
        int status = decision.Outcome == PairingOutcome.Missing ? 400 : 401;
        await WriteJsonAsync(request, status, new { error = "pairing failed", detail = decision.Detail }).ConfigureAwait(false);
    }

    private async Task HandleUploadAsync(WirelessRequest request, string sessionToken, string client, CancellationToken token)
    {
        // The trial is measured in jobs, and one sender session is one job. Checking here means a finished
        // trial refuses the upload before any bytes are read, while a session already charged keeps working
        // for every file it sends.
        if (_license is not null)
        {
            LicenseDecision decision = _license.TryStartWirelessUpload(sessionToken);
            if (!decision.Allowed)
            {
                _log.Write("Wireless upload refused: the trial has finished on this PC.", "WARN");
                Raise("reject", "Wireless upload refused: the trial has finished on the receiving PC.", false);
                await DrainBodyAsync(request, token).ConfigureAwait(false);
                await WriteJsonAsync(request, 402, new { error = "trial finished", detail = decision.Message }).ConfigureAwait(false);
                return;
            }
        }

        if (!request.Query.TryGetValue("path", out string? rawPath) || string.IsNullOrWhiteSpace(rawPath))
        {
            await DrainBodyAsync(request, token).ConfigureAwait(false);
            await WriteJsonAsync(request, 400, new { error = "missing path" }).ConfigureAwait(false);
            return;
        }

        string relative;
        try
        {
            string normalized = Uri.UnescapeDataString(rawPath).Replace('\\', '/').TrimStart('/');
            int idx = normalized.LastIndexOf('/');
            string folder = idx < 0 ? CameraRoll.RootLabel : normalized[..idx];
            string name = idx < 0 ? normalized : normalized[(idx + 1)..];
            // The offer identity is the sender's spelling (forward slashes). It is converted to the
            // platform separator only when a path is built, so a refusal or an approval matches the
            // path the operator was shown.
            relative = TransferRules.ValidateRelativePath(folder, name).Replace('\\', '/');
        }
        catch (Exception ex)
        {
            _log.Write($"Wireless upload rejected unsafe path '{rawPath}': {ex.Message}", "WARN");
            await DrainBodyAsync(request, token).ConfigureAwait(false);
            await WriteJsonAsync(request, 400, new { error = "invalid path", detail = ex.Message }).ConfigureAwait(false);
            return;
        }

        foreach (string reserved in ReservedPaths)
            if (relative.StartsWith(reserved, StringComparison.OrdinalIgnoreCase))
            {
                await DrainBodyAsync(request, token).ConfigureAwait(false);
                await WriteJsonAsync(request, 400, new { error = "reserved path" }).ConfigureAwait(false);
                return;
            }

        long declared = request.ContentLength;
        if (declared < 0)
        {
            await WriteJsonAsync(request, 411, new { error = "length required", detail = "Send the file with a Content-Length header so its size can be checked." }).ConfigureAwait(false);
            return;
        }
        if (declared > DefaultMaxFileBytes)
        {
            await WriteJsonAsync(request, 413, new { error = "file too large" }).ConfigureAwait(false);
            return;
        }

        // One in-flight upload per path: a second waiter for the same name would replace the first and
        // leave it hanging until the timeout.
        if (_waiting.ContainsKey(relative))
        {
            await DrainBodyAsync(request, token).ConfigureAwait(false);
            await WriteJsonAsync(request, 409, new { error = "already receiving this path" }).ConfigureAwait(false);
            return;
        }

        string? declaredHash = request.Header("X-Content-Sha256");
        var offer = new IncomingOffer(relative, Math.Max(0, declared), declaredHash, client, DateTimeOffset.UtcNow);
        OfferRecord record = _inbox.Offer(offer, DateTimeOffset.UtcNow);
        if (record.IsPending)
        {
            _log.Write($"Wireless offer from {client}: {relative} ({declared} bytes) is waiting for approval.");
            Raise("offer", $"{client} wants to send {relative} ({declared:N0} bytes). Approve or refuse it on this page.", true);
            OfferArrived?.Invoke(record);
            bool approved = await WaitForDecisionAsync(relative, token).ConfigureAwait(false);
            if (!approved)
            {
                _inbox.Expire(DateTimeOffset.UtcNow);
                _log.Write($"Wireless offer {relative} was not approved; nothing was written.", "WARN");
                await DrainBodyAsync(request, token).ConfigureAwait(false);
                await WriteJsonAsync(request, 403, new { error = "not approved", detail = "The operator did not approve this file." }).ConfigureAwait(false);
                return;
            }
        }
        else if (record.State != OfferState.Approved)
        {
            await DrainBodyAsync(request, token).ConfigureAwait(false);
            await WriteJsonAsync(request, 403, new { error = "refused", detail = record.Reason }).ConfigureAwait(false);
            return;
        }

        await ReceiveAsync(request, relative, client, declaredHash, sessionToken, token).ConfigureAwait(false);
    }

    private async Task<bool> WaitForDecisionAsync(string relativePath, CancellationToken token)
    {
        // The operator can decide between the offer becoming visible and this call, so check the
        // current state before waiting and again after registering — a decision must never be lost.
        OfferState? state = _inbox.StateOf(relativePath);
        if (state == OfferState.Approved) return true;
        if (state is OfferState.Refused or OfferState.Expired) return false;

        var wait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting[relativePath] = wait;
        try
        {
            state = _inbox.StateOf(relativePath);
            if (state == OfferState.Approved) return true;
            if (state is OfferState.Refused or OfferState.Expired) return false;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(ApprovalTimeout);
            using (timeout.Token.Register(() => wait.TrySetResult(_inbox.IsApproved(relativePath))))
                return await wait.Task;
        }
        finally { _waiting.TryRemove(relativePath, out _); }
    }

    private async Task ReceiveAsync(WirelessRequest request, string relative, string client, string? declaredHash, string sessionToken, CancellationToken token)
    {
        // Refuse before writing when the volume cannot hold the declared size.
        long free = FreeSpace();
        long declared = Math.Max(0, request.ContentLength);
        if (declared > 0 && free > 0 && declared > free)
        {
            await DrainBodyAsync(request, token).ConfigureAwait(false);
            await WriteJsonAsync(request, 507, new { error = "insufficient space", freeBytes = free }).ConfigureAwait(false);
            return;
        }

        string finalPath = Path.Combine(_destination, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        string stagingPath = Path.Combine(_destination, ".i-total", "wireless", Guid.NewGuid().ToString("N") + ".partial");

        long written = 0;
        string hash;
        try
        {
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (var output = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous))
            {
                byte[] buffer = new byte[1024 * 1024];
                while (written < declared)
                {
                    int wanted = (int)Math.Min(buffer.Length, declared - written);
                    int read = await request.Body.ReadAsync(buffer.AsMemory(0, wanted), token).ConfigureAwait(false);
                    if (read == 0) break;
                    written += read;
                    if (written > DefaultMaxFileBytes)
                    {
                        await output.DisposeAsync().ConfigureAwait(false);
                        TryDelete(stagingPath);
                        await WriteJsonAsync(request, 413, new { error = "file too large" }).ConfigureAwait(false);
                        return;
                    }
                    sha.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                }
                await output.FlushAsync(token).ConfigureAwait(false);
                hash = Convert.ToHexString(sha.GetHashAndReset());
            }

            if (!string.IsNullOrWhiteSpace(declaredHash) &&
                !string.Equals(declaredHash.Trim(), hash, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(stagingPath);
                _log.Write($"Wireless payload {relative} failed its declared digest; discarded.", "WARN");
                await WriteJsonAsync(request, 422, new { error = "digest mismatch", expected = declaredHash, actual = hash }).ConfigureAwait(false);
                return;
            }

            if (declared > 0 && written != declared)
            {
                TryDelete(stagingPath);
                await WriteJsonAsync(request, 422, new { error = "size mismatch", expected = declared, actual = written }).ConfigureAwait(false);
                return;
            }

            // Commit only now: the stream is closed, the digest matches and the size matches.
            // The lock covers both choosing a free name and the move, so two concurrent uploads of the
            // same path cannot both resolve to the same " (1)" name and have one overwrite the other.
            lock (_commitSync)
            {
                if (File.Exists(finalPath))
                    finalPath = TransferRules.ResolveTargetPath(_destination, relative, ConflictPolicy.RenameIncoming, File.Exists);
                File.Move(stagingPath, finalPath);
            }
        }
        catch (OperationCanceledException)
        {
            TryDelete(stagingPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(stagingPath);
            _log.Write($"Wireless receive failed for {relative}: {ex.Message}", "ERROR");
            await WriteJsonAsync(request, 500, new { error = ex.Message }).ConfigureAwait(false);
            return;
        }

        var record = new ReceivedFile(Path.GetRelativePath(_destination, finalPath), written, hash, DateTimeOffset.UtcNow, client);
        lock (_sync) _received.Add(record);
        SaveManifest();
        _license?.RecordWirelessReceipt(sessionToken, record.RelativePath, written);
        _log.Write($"Wireless received {record.RelativePath} ({written} bytes) from {client} sha256={hash}.");
        Raise("receive", $"Received {record.RelativePath} ({written:N0} bytes) from {client}; SHA-256 verified on receipt.", true);
        await WriteJsonAsync(request, 200, new { path = record.RelativePath, bytes = written, sha256 = hash }).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes half-written staging files left by a crash or a forced close. They are unapproved,
    /// unnamed fragments; keeping them would only consume disk.
    /// </summary>
    private void SweepOrphanedStaging(string stagingRoot)
    {
        try
        {
            string[] orphans = Directory.GetFiles(stagingRoot, "*.partial");
            foreach (string orphan in orphans) TryDelete(orphan);
            if (orphans.Length > 0)
                _log.Write($"Removed {orphans.Length} unfinished wireless staging file(s) from a previous run.");
        }
        catch (Exception ex) { _log.Write($"Could not sweep wireless staging files: {ex.Message}", "WARN"); }
    }

    private long FreeSpace()
    {
        try
        {
            string? root = Path.GetPathRoot(_destination);
            return string.IsNullOrEmpty(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
        }
        catch { return 0; }
    }

    private static string ClientOf(WirelessRequest request)
    {
        string agent = request.UserAgent;
        return string.IsNullOrWhiteSpace(agent) ? request.RemoteAddress : $"{request.RemoteAddress} ({Shorten(agent, 60)})";
    }

    private static string Shorten(string value, int max) => value.Length <= max ? value : value[..max] + "…";

    private void SaveManifest()
    {
        try
        {
            string path = Path.Combine(_destination, ".i-total", "wireless-received.json");
            ReceivedFile[] snapshot;
            lock (_sync) snapshot = _received.ToArray();
            LogService.ExportJson(path, new { UpdatedUtc = DateTimeOffset.UtcNow, Files = snapshot });
        }
        catch (Exception ex) { _log.Write($"Wireless manifest write failed: {ex.Message}", "WARN"); }
    }

    /// <summary>
    /// Reads and discards a payload before an early refusal. A sender has already started streaming its
    /// body; closing the socket first would surface a transport error instead of the real HTTP reason.
    /// Nothing is written to the destination, and the drain is bounded in time.
    /// </summary>
    private static async Task DrainBodyAsync(WirelessRequest request, CancellationToken token)
    {
        if (request.ContentLength <= 0) return;
        long remaining = request.ContentLength;
        byte[] buffer = new byte[64 * 1024];
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            while (remaining > 0)
            {
                int wanted = (int)Math.Min(buffer.Length, remaining);
                int read = await request.Body.ReadAsync(buffer.AsMemory(0, wanted), timeout.Token).ConfigureAwait(false);
                if (read == 0) break;
                remaining -= read;
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    /// <summary>True when a browser navigation asked for HTML rather than an API response.</summary>
    private static bool WantsHtml(WirelessRequest request) =>
        request.Header("Accept")?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>A browser asks for /favicon.ico unprompted; answer without a credential or a 401.</summary>
    private static async Task WriteNoContentAsync(WirelessRequest request)
    {
        if (request.ResponseStarted) return;
        request.ResponseStarted = true;
        const string head = "HTTP/1.1 204 No Content\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
        await request.Body.WriteAsync(Encoding.ASCII.GetBytes(head)).ConfigureAwait(false);
        await request.Body.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Serves the embedded sender page and script with a restrictive policy: no external resources, no
    /// framing, no referrer, and only same-origin connections (the upload and pairing calls).
    /// </summary>
    private static async Task WriteAssetAsync(WirelessRequest request, string contentType, string content)
    {
        if (request.ResponseStarted) return;
        request.ResponseStarted = true;
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        string head = "HTTP/1.1 200 OK\r\n" +
                      $"Content-Type: {contentType}\r\n" +
                      $"Content-Length: {bytes.Length}\r\n" +
                      "Cache-Control: no-store\r\n" +
                      "X-Content-Type-Options: nosniff\r\n" +
                      "Referrer-Policy: no-referrer\r\n" +
                      "Content-Security-Policy: default-src 'none'; script-src 'self'; style-src 'unsafe-inline'; " +
                      "connect-src 'self'; img-src 'self' data:; base-uri 'none'; form-action 'none'; frame-ancestors 'none'\r\n" +
                      "Connection: close\r\n\r\n";
        await request.Body.WriteAsync(Encoding.ASCII.GetBytes(head)).ConfigureAwait(false);
        await request.Body.WriteAsync(bytes).ConfigureAwait(false);
        await request.Body.FlushAsync().ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(WirelessRequest request, int status, object payload)
    {
        if (request.ResponseStarted) return;
        request.ResponseStarted = true;
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        string reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            401 => "Unauthorized",
            402 => "Payment Required",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            411 => "Length Required",
            413 => "Content Too Large",
            422 => "Unprocessable Content",
            500 => "Internal Server Error",
            507 => "Insufficient Storage",
            _ => "Error"
        };
        string head = $"HTTP/1.1 {status} {reason}\r\n" +
                      "Content-Type: application/json; charset=utf-8\r\n" +
                      $"Content-Length: {bytes.Length}\r\n" +
                      "Cache-Control: no-store\r\n" +
                      "X-Content-Type-Options: nosniff\r\n" +
                      "Connection: close\r\n\r\n";
        await request.Body.WriteAsync(Encoding.ASCII.GetBytes(head)).ConfigureAwait(false);
        await request.Body.WriteAsync(bytes).ConfigureAwait(false);
        await request.Body.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// One parsed HTTP/1.1 request. The body is the same buffered stream the headers came from, so a
    /// sender's payload is never copied into memory before the approval gate.
    /// </summary>
    private sealed record WirelessRequest(
        Stream Body,
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Query,
        IReadOnlyDictionary<string, string> Headers,
        long ContentLength,
        string RemoteAddress,
        string UserAgent)
    {
        public bool ResponseStarted { get; set; }

        public string? Header(string name) => Headers.TryGetValue(name, out string? value) ? value : null;
    }

    private void Raise(string kind, string message, bool success) => Activity?.Invoke(new WirelessEvent(kind, message, success));

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _cts = null;
    }
}





