namespace SessionFinder.Cli.Commands;

/// <summary>
/// Describes one diagnostic command exposed by the CLI head, for usage rendering and dispatch.
/// </summary>
/// <param name="Name">The verb typed by the user, for example <c>reindex</c>.</param>
/// <param name="Usage">The argument shape shown in the usage block.</param>
/// <param name="Description">A one-line explanation of what the command does.</param>
public sealed record CliCommandDescriptor(string Name, string Usage, string Description);
