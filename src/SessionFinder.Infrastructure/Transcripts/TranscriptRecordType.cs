namespace SessionFinder.Infrastructure.Transcripts;

/// <summary>
/// The <c>type</c> discriminator of a transcript line. Sixteen kinds exist on disk and four carry
/// anything worth indexing, so this is a flat enum rather than a polymorphic record hierarchy.
/// </summary>
public enum TranscriptRecordType
{
    /// <summary>A kind this build does not know about. Scanned, but only for its working folder.</summary>
    Unknown = 0,

    /// <summary>A turn attributed to the user. Carries human prompts, and also tool results.</summary>
    User = 1,

    /// <summary>A turn from the model. Carries text, thinking and tool calls.</summary>
    Assistant = 2,

    /// <summary>A title the user typed for the session.</summary>
    CustomTitle = 3,

    /// <summary>A generated title for the session.</summary>
    AiTitle = 4,

    /// <summary>The prompt shown in the resume banner.</summary>
    LastPrompt = 5,

    /// <summary>Pasted or referenced content. Ignored.</summary>
    Attachment = 6,

    /// <summary>Tooling notices and retry diagnostics. Ignored.</summary>
    System = 7,

    /// <summary>A permission-mode change. Ignored.</summary>
    Mode = 8,

    /// <summary>Bookkeeping for queued prompts. Ignored.</summary>
    QueueOperation = 9,

    /// <summary>Internal latch state. Ignored.</summary>
    AtisLatch = 10,

    /// <summary>Ownership metadata for a bridged session. Ignored.</summary>
    BridgeSession = 11,

    /// <summary>A pull request produced by the session. Ignored.</summary>
    PrLink = 12,

    /// <summary>A frame reference. Ignored.</summary>
    FrameLink = 13,

    /// <summary>A snapshot of tracked files. Ignored, and among the largest records on disk.</summary>
    FileHistorySnapshot = 14,

    /// <summary>A diff against a file snapshot. Ignored.</summary>
    FileHistoryDelta = 15,
}
