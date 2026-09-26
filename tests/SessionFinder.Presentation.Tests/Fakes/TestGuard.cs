using Microsoft.Extensions.Logging.Abstractions;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Presentation.Tests.Fakes;

/// <summary>
/// Builds the real command guard over a recording notifier. The guard is the behaviour under test
/// in its own file; everywhere else it is wiring, and a fake of it would let a view model pass a
/// test the application would fail.
/// </summary>
internal static class TestGuard
{
    public static AppCommandGuard Over(IUserNotifier notifier) =>
        new(notifier, NullLogger<AppCommandGuard>.Instance);
}
