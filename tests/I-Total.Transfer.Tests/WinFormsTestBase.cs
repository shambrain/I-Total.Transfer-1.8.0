using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// Base class for tests that create real WinForms controls.
///
/// <para>
/// Creating a control can install a <c>WindowsFormsSynchronizationContext</c> on the test thread, and that
/// context only runs posted work while a message pump is idle. Test threads never pump, so leaving one
/// installed is contagious: any later test that <c>await</c>s, or waits for a <c>Progress&lt;T&gt;</c>
/// callback, never resumes, and the whole run is aborted as a hang rather than failing a single test. That
/// is exactly what happened here — the transfer and stress suites died only when a UI test class had run
/// before them.
/// </para>
///
/// <para>
/// The context is therefore snapshotted before each test and restored afterwards, so a control-creating
/// test cannot change how every subsequent test behaves.
/// </para>
/// </summary>
public abstract class WinFormsTestBase
{
    private SynchronizationContext? _contextBeforeTest;

    [TestInitialize]
    public void CaptureSynchronizationContext() => _contextBeforeTest = SynchronizationContext.Current;

    [TestCleanup]
    public void RestoreSynchronizationContext()
    {
        if (!ReferenceEquals(SynchronizationContext.Current, _contextBeforeTest))
            SynchronizationContext.SetSynchronizationContext(_contextBeforeTest);
    }
}
