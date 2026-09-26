using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.Settings;

namespace SessionFinder.Core.Tests.Features.Settings;

public sealed class FinderSettingsTests
{
    [Fact]
    public void From_NoHotkeyWasEverConfigured_ShowsTheOneTheApplicationAsksForFirst()
    {
        var settings = FinderSettings.From(new ShellOptions(), new SearchOptions(), new LogLevelOptions());

        settings.Hotkey.Should().Be(ShellOptions.DefaultHotkey);
    }

    [Fact]
    public void From_TheLogLevelIsDebug_ReportsVerboseLogging()
    {
        var logLevel = new LogLevelOptions { Default = LogLevelOptions.VerboseLevel };

        var settings = FinderSettings.From(new ShellOptions(), new SearchOptions(), logLevel);

        settings.VerboseLogging.Should().BeTrue();
    }

    [Fact]
    public void From_TheLogLevelIsTheOrdinaryOne_ReportsNoVerboseLogging()
    {
        var logLevel = new LogLevelOptions { Default = LogLevelOptions.NormalLevel };

        var settings = FinderSettings.From(new ShellOptions(), new SearchOptions(), logLevel);

        settings.VerboseLogging.Should().BeFalse();
    }

    [Fact]
    public void From_WeightsWereTuned_CarriesThemThrough()
    {
        var search = new SearchOptions { TitleWeight = 11.0 };

        var settings = FinderSettings.From(new ShellOptions(), search, new LogLevelOptions());

        settings.ChunkWeights.Title.Should().Be(11.0);
    }

    [Fact]
    public void Validate_EverythingIsInRange_Succeeds()
    {
        var settings = Defaults();

        settings.Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_TheHotkeyIsBlank_Fails()
    {
        var settings = Defaults() with { Hotkey = "  " };

        settings.Validate().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoResultsWouldEverBeShown_Fails()
    {
        var settings = Defaults() with { MaxResults = 0 };

        settings.Validate().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_MoreResultsThanAListCanShow_Fails()
    {
        var settings = Defaults() with { MaxResults = 5_000 };

        settings.Validate().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_TheHalfLifeIsZero_Fails()
    {
        var settings = Defaults() with { RecencyHalfLifeDays = 0 };

        settings.Validate().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_AWeightIsNegative_Fails()
    {
        var settings = Defaults() with { ChunkWeights = new ChunkWeights(-1, 1, 1, 1, 1) };

        settings.Validate().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_AWeightIsRejected_NamesTheSettingRatherThanTheType()
    {
        var settings = Defaults() with { RecencyWeight = -0.5 };

        settings.Validate().Error!.Message.Should().Contain("weight");
    }

    private static FinderSettings Defaults() =>
        FinderSettings.From(new ShellOptions(), new SearchOptions(), new LogLevelOptions());
}
