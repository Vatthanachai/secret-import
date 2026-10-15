using Avalonia.Headless;

[assembly: AssemblyFixture(typeof(SecretImport.Desktop.Tests.HeadlessSession))]

namespace SecretImport.Desktop.Tests;

/// <summary>
/// One Avalonia headless session for the whole test assembly. UI code must run through <see cref="RunAsync"/>,
/// which dispatches onto the Avalonia UI thread.
/// </summary>
public sealed class HeadlessSession : IAsyncDisposable
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));

    public Task RunAsync(Action action)
    {
        return _session.Dispatch(action, TestContext.Current.CancellationToken);
    }

    // Use DisposeAsync: the synchronous HeadlessUnitTestSession.Dispose blocks forever on xUnit's fixture teardown.
    public ValueTask DisposeAsync()
    {
        return _session.DisposeAsync();
    }
}
