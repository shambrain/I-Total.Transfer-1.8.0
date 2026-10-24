namespace ITotalTransfer.Core;

/// <summary>
/// Keeps one backend bound to the phone it selected. When both an iPhone and an Android phone are on
/// the portable-device bus, re-enumerating after the selected phone briefly stops answering can return
/// the other handset. Rebinding at that moment would scan (and cache, and copy) one phone's files under
/// the other phone's name, so a bound guard refuses the switch instead.
/// </summary>
public sealed class DeviceSelectionGuard
{
    public string? DeviceId { get; private set; }
    public string? FriendlyName { get; private set; }
    public DeviceFamily Family { get; private set; } = DeviceFamily.Unknown;

    public bool IsBound => DeviceId is not null;

    /// <summary>
    /// Binds the first selected device. A later, different id is refused rather than adopted. A null
    /// <paramref name="id"/> is never passed here; callers report the missing-device case separately.
    /// </summary>
    public void Bind(string id, string friendlyName, DeviceFamily family)
    {
        if (DeviceId is null)
        {
            DeviceId = id;
            FriendlyName = friendlyName;
            Family = family;
            return;
        }

        if (!string.Equals(DeviceId, id, StringComparison.Ordinal))
            throw new InvalidOperationException(GoneMessage(friendlyName));
    }

    /// <summary>True when the guard is bound and a different device is now being offered.</summary>
    public bool IsDifferent(string id) =>
        DeviceId is not null && !string.Equals(DeviceId, id, StringComparison.Ordinal);

    /// <summary>
    /// An honest message for a session whose selected phone went away. It never suggests that the other
    /// attached phone was read, because it was not.
    /// </summary>
    public string GoneMessage(string? otherAttached)
    {
        string selected = string.IsNullOrWhiteSpace(FriendlyName)
            ? "the phone selected for this session"
            : FriendlyName;
        string message = "The phone selected for this session (" + selected + ") is no longer answering.";
        message += string.IsNullOrWhiteSpace(otherAttached)
            ? " The other attached phone, if any, was not used."
            : " Windows now lists " + otherAttached +
              ", but this session will not switch phones automatically; that phone was not used.";
        return message + " Reconnect the selected phone, then choose Check device and scan again.";
    }
}
