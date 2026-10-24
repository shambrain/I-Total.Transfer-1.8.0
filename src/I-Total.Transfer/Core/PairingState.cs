using System.Security.Cryptography;
using System.Text;

namespace ITotalTransfer.Core;

/// <summary>How a session token came to exist, for the log and the received-file record.</summary>
public enum PairingOutcome
{
    /// <summary>The request carried no usable token.</summary>
    Missing,

    /// <summary>The token was well formed but is not, or is no longer, a live session.</summary>
    Invalid,

    /// <summary>The pairing secret had already been spent; a pairing secret is single-use.</summary>
    AlreadyUsed,

    /// <summary>The pairing secret was valid and a session was opened; the caller must show the token once.</summary>
    Paired,

    /// <summary>A live session token was presented and accepted.</summary>
    Accepted,
}

/// <summary>Result of presenting a credential to the pairing state.</summary>
public sealed record PairingDecision(PairingOutcome Outcome, string? SessionToken, string? Detail)
{
    public bool IsAccepted => Outcome is PairingOutcome.Paired or PairingOutcome.Accepted;
}

/// <summary>
/// The credential state of one listening session: a short-lived, single-use pairing secret, and the
/// session tokens minted from it.
///
/// <para>
/// The first implementation used one token for everything: it was shown in the URL, it never rotated,
/// and it stayed valid for its whole lifetime no matter how many files were sent. That is a bearer
/// token in a URL — which lands in browser history, proxy logs and screenshots — that also happens to
/// authorise unlimited writes. The plan for this feature asks for a single-use pairing secret, token
/// rotation and expiry, so the two jobs are now separated:
/// </para>
/// <list type="number">
/// <item>A <b>pairing secret</b> is shown to the operator (QR or URL). It is single-use and expires.</item>
/// <item>Redeeming it mints a <b>session token</b>, which is what every upload carries.</item>
/// <item>Session tokens are bounded in number and time, can be revoked, and rotate on request.</item>
/// </list>
///
/// <para>
/// Comparison is constant-time, tokens are generated from <see cref="RandomNumberGenerator"/>, and the
/// state is pure (no clock of its own) so every rule here is unit-testable without a network.
/// </para>
/// </summary>
public sealed class PairingState
{
    /// <summary>A session token is valid for one hour by default; a long copy can always rotate.</summary>
    public static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromHours(1);

    /// <summary>At most this many live sessions, so a leaked secret cannot open unlimited clients.</summary>
    public const int MaxSessions = 8;

    private sealed record Session(string Token, DateTimeOffset ExpiresUtc, string Client)
    {
        public int Uses { get; set; }
    }

    private readonly object _sync = new();
    private readonly List<Session> _sessions = [];
    private string? _secret;
    private DateTimeOffset _secretExpiresUtc;
    private bool _secretUsed;
    private DateTimeOffset _lastRotatedUtc = DateTimeOffset.MinValue;

    /// <summary>Lifetime of a session token minted by this state.</summary>
    public TimeSpan SessionLifetime { get; init; } = DefaultSessionLifetime;

    /// <summary>Creates a fresh pairing secret, replacing (and invalidating) any previous one.</summary>
    public string CreateSecret(DateTimeOffset now, TimeSpan lifetime)
    {
        string secret = NewToken();
        lock (_sync)
        {
            _secret = secret;
            _secretExpiresUtc = now.Add(lifetime);
            _secretUsed = false;
        }
        return secret;
    }

    /// <summary>True when a secret exists and has neither expired nor been spent.</summary>
    public bool HasLiveSecret(DateTimeOffset now)
    {
        lock (_sync) return _secret is not null && !_secretUsed && now < _secretExpiresUtc;
    }

    /// <summary>When the current secret stops being usable, or null when there is none.</summary>
    public DateTimeOffset? SecretExpiresUtc { get { lock (_sync) return _secret is null ? null : _secretExpiresUtc; } }

    /// <summary>True once the current secret has been redeemed.</summary>
    public bool SecretWasUsed { get { lock (_sync) return _secretUsed; } }

    /// <summary>How many session tokens are currently live.</summary>
    public int LiveSessionCount(DateTimeOffset now)
    {
        lock (_sync)
        {
            Prune(now);
            return _sessions.Count;
        }
    }

    /// <summary>
    /// Redeems a pairing secret. This is the only method that can spend the secret, and only the
    /// <c>/pair</c> endpoint calls it — an upload that presents the secret is not a pairing request and
    /// must not be able to turn one into a live session.
    /// </summary>
    public PairingDecision Redeem(string? secret, string client, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return new PairingDecision(PairingOutcome.Missing, null, "No pairing secret was supplied.");

        string value = secret.Trim();
        lock (_sync)
        {
            Prune(now);

            if (_secret is null || !FixedEquals(value, _secret))
                return new PairingDecision(PairingOutcome.Invalid, null, "The pairing secret is not recognised.");

            if (_secretUsed)
                return new PairingDecision(PairingOutcome.AlreadyUsed, null,
                    "That pairing secret has already been used. Ask for a new ticket.");

            if (now >= _secretExpiresUtc)
                return new PairingDecision(PairingOutcome.Invalid, null,
                    "That pairing secret has expired. Ask for a new ticket.");

            // Single use: the secret dies the moment it is redeemed.
            _secretUsed = true;
            if (_sessions.Count >= MaxSessions)
                _sessions.RemoveAt(0);

            var session = new Session(NewToken(), now.Add(SessionLifetime), client);
            _sessions.Add(session);
            _lastRotatedUtc = now;
            return new PairingDecision(PairingOutcome.Paired, session.Token,
                "Pairing secret redeemed; this session token is shown once.");
        }
    }

    /// <summary>
    /// Checks a session token. It can never redeem a secret: a credential that happens to match the
    /// outstanding secret is not a session, and is reported as unrecognised.
    /// </summary>
    public PairingDecision Authorize(string? token, string client, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new PairingDecision(PairingOutcome.Missing, null, "No session token was supplied.");

        string value = token.Trim();
        lock (_sync)
        {
            Prune(now);
            foreach (Session session in _sessions)
            {
                if (!FixedEquals(value, session.Token)) continue;
                if (now >= session.ExpiresUtc)
                    return new PairingDecision(PairingOutcome.Invalid, null,
                        "This session has expired. Ask for a new pairing ticket.");
                session.Uses++;
                return new PairingDecision(PairingOutcome.Accepted, session.Token, "Session accepted.");
            }

            return new PairingDecision(PairingOutcome.Invalid, null, "The session token is not recognised.");
        }
    }

    /// <summary>
    /// Replaces one live session token with a fresh one. Rotation is how a sender that is about to run
    /// a long transfer avoids the session expiring mid-copy without ever widening the lifetime.
    /// </summary>
    public string? Rotate(string currentToken, DateTimeOffset now)
    {
        lock (_sync)
        {
            Prune(now);
            for (int i = 0; i < _sessions.Count; i++)
            {
                if (!FixedEquals(currentToken ?? "", _sessions[i].Token)) continue;
                var rotated = _sessions[i] with { Token = NewToken(), ExpiresUtc = now.Add(SessionLifetime) };
                _sessions[i] = rotated;
                _lastRotatedUtc = now;
                return rotated.Token;
            }
            return null;
        }
    }

    /// <summary>Revokes every session and the outstanding secret (Stop, or an operator's choice).</summary>
    public void RevokeAll()
    {
        lock (_sync)
        {
            _sessions.Clear();
            _secret = null;
            _secretUsed = false;
        }
    }

    /// <summary>Revokes one session token; true when it was live.</summary>
    public bool Revoke(string token)
    {
        lock (_sync) return _sessions.RemoveAll(s => FixedEquals(token ?? "", s.Token)) > 0;
    }

    /// <summary>When the most recent session token was minted or rotated (for the UI).</summary>
    public DateTimeOffset LastRotatedUtc { get { lock (_sync) return _lastRotatedUtc; } }

    /// <summary>An unguessable 32-character lowercase hex token (128 bits).</summary>
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private void Prune(DateTimeOffset now) => _sessions.RemoveAll(s => now >= s.ExpiresUtc);

    /// <summary>Length-independent, allocation-free-enough constant-time comparison.</summary>
    private static bool FixedEquals(string a, string b)
    {
        byte[] left = Encoding.UTF8.GetBytes(a);
        byte[] right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
