using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// A switch for tests that talk to a real handset.
///
/// <para>
/// The deterministic suite must pass on any machine, with or without a phone plugged in, and must never
/// wait on a device that may be busy, absent, locked or enumerating tens of thousands of objects. Tests
/// that need real hardware declare it here and are reported as <c>Inconclusive</c> (not failed, not
/// silently passed) unless <c>ITOTAL_HARDWARE_TESTS=1</c> is set. This mirrors how the app itself treats
/// device access: a capability that is not available is reported, never faked.
/// </para>
///
/// <example>
/// <code>
/// $env:ITOTAL_HARDWARE_TESTS = "1"; dotnet test tests\I-Total.Transfer.Tests -c Release
/// </code>
/// </example>
/// </summary>
internal static class Hardware
{
    public const string SwitchName = "ITOTAL_HARDWARE_TESTS";

    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable(SwitchName), "1", StringComparison.Ordinal);

    /// <summary>Marks the calling test as inconclusive unless hardware testing was requested.</summary>
    public static void Require() => Assert.Inconclusive(
        $"This test needs a physical handset. Set {SwitchName}=1 with the device attached and unlocked to run it.");

    /// <summary>
    /// Runs blocking device work on a background thread and fails — instead of hanging the whole run —
    /// if the device does not answer inside the allowance.
    /// </summary>
    public static T Bounded<T>(string what, TimeSpan allowance, Func<T> work)
    {
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception ex) { failure = ex; }
        })
        {
            IsBackground = true,
            Name = "hardware-" + what
        };
        thread.Start();

        Assert.IsTrue(thread.Join(allowance),
            $"'{what}' did not answer within {allowance.TotalSeconds:N0}s. A device call must never block the run.");
        if (failure is not null)
            throw new AssertFailedException($"'{what}' threw: {failure.Message}", failure);
        return result!;
    }
}
