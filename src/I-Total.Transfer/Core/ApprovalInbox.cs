namespace ITotalTransfer.Core;

/// <summary>What the receiver knows about an offer before any byte is written to the destination.</summary>
public sealed record IncomingOffer(
    string RelativePath,
    long DeclaredBytes,
    string? DeclaredSha256,
    string Client,
    DateTimeOffset ReceivedUtc)
{
    /// <summary>True when the sender declared a digest the receiver can check on arrival.</summary>
    public bool HasDeclaredHash => !string.IsNullOrWhiteSpace(DeclaredSha256);
}

/// <summary>Lifecycle of one offer in the approval inbox.</summary>
public enum OfferState { Pending, Approved, Refused, Expired }

/// <summary>One offer with its decision, as the UI and the log see it.</summary>
public sealed record OfferRecord(IncomingOffer Offer, OfferState State, string Reason)
{
    public bool IsPending => State == OfferState.Pending;
}

/// <summary>
/// The approval gate between a paired sender and the operator's disk.
///
/// <para>
/// The plan for this feature is explicit: a wireless session must <em>show the receiving device and
/// files, and require user approval</em>. The first implementation wrote every authenticated POST
/// straight to the destination, which means a paired client could fill the disk and choose the file
/// names without the operator ever seeing a filename. This inbox is that missing step, and it is a
/// pure state machine so the rules can be tested without a network:
/// </para>
/// <list type="bullet">
/// <item>An offer is held <see cref="OfferState.Pending"/> until an operator decides.</item>
/// <item>Only an <see cref="OfferState.Approved"/> offer may be committed.</item>
/// <item>A refusal is remembered by path, so a refused file is not silently accepted on retry.</item>
/// <item>Pending offers expire, so an abandoned session cannot leave the gate open indefinitely.</item>
/// </list>
///
/// <para>
/// When the operator has switched automatic approval on, offers are approved as they arrive — that is
/// an explicit, visible preference, not a silent default.
/// </para>
/// </summary>
public sealed class ApprovalInbox
{
    /// <summary>How long a pending offer waits before it is refused by default.</summary>
    public static readonly TimeSpan DefaultOfferLifetime = TimeSpan.FromMinutes(30);

    private readonly object _sync = new();
    private readonly List<OfferRecord> _offers = [];
    private readonly HashSet<string> _refused = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When true, every offer is approved on arrival. The operator opts into this.</summary>
    public bool AutoApprove { get; set; }

    /// <summary>Lifetime of a pending offer.</summary>
    public TimeSpan OfferLifetime { get; init; } = DefaultOfferLifetime;

    /// <summary>Every offer, oldest first.</summary>
    public IReadOnlyList<OfferRecord> Offers { get { lock (_sync) return _offers.ToArray(); } }

    /// <summary>Offers still waiting for a decision.</summary>
    public IReadOnlyList<OfferRecord> Pending
    {
        get { lock (_sync) return _offers.Where(o => o.IsPending).ToArray(); }
    }

    /// <summary>
    /// Records an offer and returns its decision. A path the operator already refused is refused
    /// again without asking, so a sender cannot out-wait a refusal by retrying.
    /// </summary>
    public OfferRecord Offer(IncomingOffer offer, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(offer);
        lock (_sync)
        {
            Expire(now);

            if (_refused.Contains(offer.RelativePath))
            {
                var refused = new OfferRecord(offer, OfferState.Refused,
                    "This file was refused earlier in this session; it will not be accepted on retry.");
                _offers.Add(refused);
                return refused;
            }

            var record = new OfferRecord(offer, AutoApprove ? OfferState.Approved : OfferState.Pending,
                AutoApprove ? "Approved automatically by the operator's preference." : "Waiting for the operator to approve.");
            _offers.Add(record);
            return record;
        }
    }

    /// <summary>Approves a pending offer by relative path; returns the updated record or null.</summary>
    public OfferRecord? Approve(string relativePath, DateTimeOffset now) => Decide(relativePath, true, "Approved by the operator.", now);

    /// <summary>Refuses an offer by relative path; the path stays refused for the session.</summary>
    public OfferRecord? Refuse(string relativePath, DateTimeOffset now)
    {
        OfferRecord? record = Decide(relativePath, false, "Refused by the operator.", now);
        if (record is not null) lock (_sync) _refused.Add(record.Offer.RelativePath);
        return record;
    }

    /// <summary>Approves every pending offer. Returns how many changed.</summary>
    public int ApproveAll(DateTimeOffset now)
    {
        lock (_sync)
        {
            Expire(now);
            int changed = 0;
            for (int i = 0; i < _offers.Count; i++)
            {
                if (!_offers[i].IsPending) continue;
                _offers[i] = _offers[i] with { State = OfferState.Approved, Reason = "Approved by the operator." };
                changed++;
            }
            return changed;
        }
    }

    /// <summary>Refuses every pending offer. Returns how many changed.</summary>
    public int RefuseAll(DateTimeOffset now)
    {
        lock (_sync)
        {
            Expire(now);
            int changed = 0;
            for (int i = 0; i < _offers.Count; i++)
            {
                if (!_offers[i].IsPending) continue;
                _offers[i] = _offers[i] with { State = OfferState.Refused, Reason = "Refused by the operator." };
                _refused.Add(_offers[i].Offer.RelativePath);
                changed++;
            }
            return changed;
        }
    }

    /// <summary>True when the offer for this path may be committed.</summary>
    public bool IsApproved(string relativePath)
    {
        lock (_sync)
            return _offers.Any(o => o.State == OfferState.Approved &&
                                    string.Equals(o.Offer.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The most recent decision for a path, or null when the path was never offered. The receiver
    /// needs this to close the race between an offer becoming visible and its waiter being registered:
    /// the operator can decide in that window, and the decision must not be lost.
    /// </summary>
    public OfferState? StateOf(string relativePath)
    {
        lock (_sync)
        {
            for (int i = _offers.Count - 1; i >= 0; i--)
                if (string.Equals(_offers[i].Offer.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
                    return _offers[i].State;
            return null;
        }
    }

    /// <summary>Marks pending offers older than the lifetime as expired. Safe to call repeatedly.</summary>
    public int Expire(DateTimeOffset now)
    {
        lock (_sync)
        {
            int changed = 0;
            for (int i = 0; i < _offers.Count; i++)
            {
                if (!_offers[i].IsPending) continue;
                if (now - _offers[i].Offer.ReceivedUtc < OfferLifetime) continue;
                _offers[i] = _offers[i] with { State = OfferState.Expired, Reason = "No decision was made before the offer expired." };
                changed++;
            }
            return changed;
        }
    }

    /// <summary>Forgets every decision. Called when the receiver stops.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _offers.Clear();
            _refused.Clear();
        }
    }

    private OfferRecord? Decide(string relativePath, bool approve, string reason, DateTimeOffset now)
    {
        lock (_sync)
        {
            Expire(now);
            for (int i = 0; i < _offers.Count; i++)
            {
                if (!string.Equals(_offers[i].Offer.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)) continue;
                if (_offers[i].State == OfferState.Approved && approve) return _offers[i];
                if (_offers[i].State is OfferState.Refused or OfferState.Expired) return _offers[i];
                _offers[i] = _offers[i] with { State = approve ? OfferState.Approved : OfferState.Refused, Reason = reason };
                return _offers[i];
            }
            return null;
        }
    }
}
