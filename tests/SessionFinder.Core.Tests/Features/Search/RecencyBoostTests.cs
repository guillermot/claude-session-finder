using SessionFinder.Core.Features.Search;

namespace SessionFinder.Core.Tests.Features.Search;

public sealed class RecencyBoostTests
{
    private const double Weight = 0.6;
    private const double DecayDays = 30.0;

    private static readonly DateTimeOffset Now = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Multiplier_SessionTouchedRightNow_IsOnePlusTheWeight()
    {
        var multiplier = RecencyBoost.Multiplier(Now, Now, Weight, DecayDays);

        multiplier.Should().BeApproximately(1.6, 1e-9);
    }

    [Fact]
    public void Multiplier_SessionOneDecayPeriodOld_HasLostAFactorOfE()
    {
        var multiplier = RecencyBoost.Multiplier(Now.AddDays(-DecayDays), Now, Weight, DecayDays);

        multiplier.Should().BeApproximately(1.0 + (Weight / Math.E), 1e-9);
    }

    [Fact]
    public void Multiplier_NewerSession_RanksAboveAnOlderOne()
    {
        var newer = RecencyBoost.Multiplier(Now.AddDays(-1), Now, Weight, DecayDays);
        var older = RecencyBoost.Multiplier(Now.AddDays(-90), Now, Weight, DecayDays);

        newer.Should().BeGreaterThan(older);
    }

    [Fact]
    public void Multiplier_VeryOldSession_ApproachesNoBoost()
    {
        var multiplier = RecencyBoost.Multiplier(Now.AddYears(-5), Now, Weight, DecayDays);

        multiplier.Should().BeApproximately(RecencyBoost.NoBoost, 1e-6);
    }

    [Fact]
    public void Multiplier_ActivityUnknown_IsNeutral()
    {
        var multiplier = RecencyBoost.Multiplier(null, Now, Weight, DecayDays);

        multiplier.Should().Be(RecencyBoost.NoBoost);
    }

    [Fact]
    public void Multiplier_TimestampInTheFuture_IsCappedAtTheZeroAgeValue()
    {
        var multiplier = RecencyBoost.Multiplier(Now.AddDays(30), Now, Weight, DecayDays);

        multiplier.Should().BeApproximately(1.0 + Weight, 1e-9);
    }

    [Fact]
    public void Multiplier_WeightTurnedOff_IsNeutral()
    {
        var multiplier = RecencyBoost.Multiplier(Now, Now, weight: 0, DecayDays);

        multiplier.Should().Be(RecencyBoost.NoBoost);
    }

    [Fact]
    public void Multiplier_DecayPeriodTurnedOff_IsNeutral()
    {
        var multiplier = RecencyBoost.Multiplier(Now, Now, Weight, decayDays: 0);

        multiplier.Should().Be(RecencyBoost.NoBoost);
    }
}
