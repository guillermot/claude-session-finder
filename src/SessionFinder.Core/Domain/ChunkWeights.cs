namespace SessionFinder.Core.Domain;

/// <summary>
/// How much a lexical match counts for, depending on what the matching text was.
/// </summary>
/// <remarks>
/// <para>
/// A hit in a title is a far stronger signal than a hit somewhere in an assistant answer: titles
/// are short and deliberate, whereas an assistant turn is long, generated, and repeats the
/// vocabulary of everything around it. Without a weight per kind the longest sessions win every
/// query simply by containing more words.
/// </para>
/// <para>
/// The weights are policy, not schema. They are applied at query time and carried into the index
/// reader with every request, so they can be retuned without rebuilding anything.
/// </para>
/// </remarks>
/// <param name="Title">Weight of a match in the resolved session title.</param>
/// <param name="Folder">Weight of a match in the working folder and git branch.</param>
/// <param name="LastPrompt">Weight of a match in the prompt shown on the resume banner.</param>
/// <param name="UserPrompt">Weight of a match in a human prompt.</param>
/// <param name="AssistantText">Weight of a match in an assistant turn.</param>
public sealed record ChunkWeights(
    double Title,
    double Folder,
    double LastPrompt,
    double UserPrompt,
    double AssistantText)
{
    /// <summary>Starting weight of a title match.</summary>
    public const double DefaultTitleWeight = 8.0;

    /// <summary>Starting weight of a folder or branch match.</summary>
    public const double DefaultFolderWeight = 2.0;

    /// <summary>Starting weight of a match in the resume banner prompt.</summary>
    public const double DefaultLastPromptWeight = 3.0;

    /// <summary>Starting weight of a match in a human prompt.</summary>
    public const double DefaultUserPromptWeight = 1.5;

    /// <summary>Starting weight of a match in an assistant turn.</summary>
    public const double DefaultAssistantTextWeight = 0.6;

    /// <summary>
    /// Weight given to text the parser did not classify. Nothing writes such a chunk today; the
    /// value exists so an unweighted kind ranks low instead of silently scoring zero.
    /// </summary>
    public const double UnclassifiedWeight = 1.0;

    /// <summary>The tuned starting point, used when nothing is configured.</summary>
    public static ChunkWeights Default { get; } = new(
        DefaultTitleWeight,
        DefaultFolderWeight,
        DefaultLastPromptWeight,
        DefaultUserPromptWeight,
        DefaultAssistantTextWeight);

    /// <summary>
    /// Returns the weight that applies to one kind of chunk.
    /// </summary>
    /// <param name="kind">What the matching text was.</param>
    /// <returns>The multiplier applied to that chunk's lexical relevance.</returns>
    public double WeightFor(ChunkKind kind) => kind switch
    {
        ChunkKind.Title => Title,
        ChunkKind.Folder => Folder,
        ChunkKind.LastPrompt => LastPrompt,
        ChunkKind.UserPrompt => UserPrompt,
        ChunkKind.AssistantText => AssistantText,
        _ => UnclassifiedWeight,
    };
}
