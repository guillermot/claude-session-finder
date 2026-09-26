using SessionFinder.Core.Abstractions;

namespace SessionFinder.Presentation.Tests.Fakes;

internal sealed class FakeApplicationPaths : IApplicationPaths
{
    public string SettingsFilePath { get; init; } = @"C:\settings\settings.json";

    public string IndexFilePath { get; init; } = @"C:\index\index.db";

    public string LogFolderPath { get; init; } = @"C:\logs";
}
