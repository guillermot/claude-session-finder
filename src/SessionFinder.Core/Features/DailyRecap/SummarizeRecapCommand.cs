namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Asks for a recap to be written up as stand-up bullets.
/// </summary>
/// <param name="Recap">The recap to summarise.</param>
public sealed record SummarizeRecapCommand(DailyRecapResult Recap);
