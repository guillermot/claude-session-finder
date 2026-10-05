namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// One working day, broken down by project.
/// </summary>
/// <param name="Day">The working day.</param>
/// <param name="Projects">The projects worked on, the busiest first.</param>
public sealed record DayRecap(DateOnly Day, IReadOnlyList<ProjectRecap> Projects)
{
    /// <summary>Whether anything happened that day.</summary>
    public bool HasActivity => Projects.Count > 0;
}
