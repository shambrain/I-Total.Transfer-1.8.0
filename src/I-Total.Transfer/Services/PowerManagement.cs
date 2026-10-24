using System.Runtime.InteropServices;

namespace ITotalTransfer.Services;

internal static class PowerManagement
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint executionState);

    public static void KeepAwake(bool enabled)
    {
        uint state = enabled ? EsContinuous | EsSystemRequired : EsContinuous;
        uint result = SetThreadExecutionState(state);
        if (result == 0) throw new InvalidOperationException($"Windows could not update the power state (Win32 error {Marshal.GetLastWin32Error()}).");
    }
}
