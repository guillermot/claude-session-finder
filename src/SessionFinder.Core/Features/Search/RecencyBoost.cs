namespace SessionFinder.Core.Features.Search;

/// <summary>
/// Turns the age of a session into the multiplier applied to its lexical relevance.
/// </summary>
/// <remarks>
/// <para>
/// This is the one piece of ranking that is not about the text. Searching your own history is
/// almost always about work you were recently doing, so of two sessions that mention a term
/// equally often the newer one is nearly always the wanted one — but a strong enough textual
/// match still has to be able to beat a weak match from this morning. A bounded multiplier does
/// exactly that: it can reorder near-ties and cannot overturn a real difference in relevance.
/// </para>
/// <para>
/// It is applied here rather than in the query so the curve can be retuned, or switched off, with
/// no change to the stored index.
/// </para>
/// </remarks>
public static class RecencyBoost
{
    /// <summary>The multiplier for a session that earns no bonus at all.</summary>
    public const double NoBoost = 1.0;

    /// <summary>
    /// Computes the multiplier for one session as <c>1 + weight * exp(-ageInDays / decayInDays)</c>,
    /// which decays smoothly from <c>1 + weight</c> at zero age towards <see cref="NoBoost"/>.
    /// </summary>
    /// <param name="lastActivity">When the session was last written to, if that is known.</param>
    /// <param name="now">The current time.</param>
    /// <param name="weight">The bonus at zero age. Zero or less disables the boost.</param>
    /// <param name="decayDays">
    /// How many days it takes for the bonus to fall by a factor of e. Zero or less disables the boost.
    /// </param>
    /// <returns>
    /// A multiplier between <see cref="NoBoost"/> and <c>1 + weight</c>. A session whose activity
    /// is unknown, or whose timestamp is in the future, earns no bonus and no penalty.
    /// </returns>
    public static double Multiplier(
        DateTimeOffset? lastActivity,
        DateTimeOffset now,
        double weight,
        double decayDays)
    {
        if (lastActivity is not { } activity || weight <= 0 || decayDays <= 0)
        {
            return NoBoost;
        }

        var ageInDays = Math.Max(0, (now - activity).TotalDays);

        return NoBoost + (weight * Math.Exp(-ageInDays / decayDays));
    }
}
