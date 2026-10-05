using SessionFinder.Cli.Commands;

namespace SessionFinder.Cli;

/// <summary>
/// Entry point of the diagnostic head. Every milestone after the skeleton adds one command;
/// the head exists from the start so the engine is verifiable without a user interface.
/// </summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 64;
    private const int ExitNotImplemented = 69;
    private const int ExitCancelled = 130;
    private const string ProductName = "Claude Session Finder";
    private const string ExecutableName = "finder";
    private const string ParseCommandName = "parse";
    private const string ReindexCommandName = "reindex";
    private const string SearchCommandName = "search";
    private const string WatchCommandName = "watch";
    private const string StatusCommandName = "status";
    private const string RecapCommandName = "recap";

    private static async Task<int> Main(string[] args)
    {
        UseUnicodeOutput();

        var verb = args.Length > 0 ? args[0] : null;
        var command = CliCommandCatalog.Find(verb);

        if (command is null)
        {
            WriteUsage();
            return verb is null ? ExitSuccess : ExitUsage;
        }

        using var cancellation = new ConsoleCancellation();

        try
        {
            return await DispatchAsync(command, args[1..], cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Cancelled.").ConfigureAwait(false);
            return ExitCancelled;
        }
    }

    private static async Task<int> DispatchAsync(
        CliCommandDescriptor command,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        switch (command.Name)
        {
            case ParseCommandName:
                return await ParseCommand.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
            case ReindexCommandName:
                return await ReindexCommand.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
            case SearchCommandName:
                return await SearchCommand.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
            case WatchCommandName:
                return await WatchCommand.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
            case StatusCommandName:
                return await StatusCommand.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
            case RecapCommandName:
                return await RecapCommand.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
            default:
                WriteNotWiredYet(command);
                return ExitNotImplemented;
        }
    }

    /// <summary>
    /// Search results carry accented words, an ellipsis and folder paths straight from the
    /// transcripts, and the default console code page renders them as noise. The diagnostic head
    /// is read by a human, so the output has to survive the console.
    /// </summary>
    private static void UseUnicodeOutput()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
    }

    private static void WriteUsage()
    {
        Console.WriteLine(ProductName);
        Console.WriteLine();
        Console.WriteLine($"Usage: {ExecutableName} <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");

        var widestUsage = CliCommandCatalog.All.Max(command => command.Usage.Length);

        foreach (var command in CliCommandCatalog.All)
        {
            Console.WriteLine($"  {command.Usage.PadRight(widestUsage)}  {command.Description}");
        }

        Console.WriteLine();
    }

    private static void WriteNotWiredYet(CliCommandDescriptor command)
    {
        Console.Error.WriteLine($"'{command.Name}' is declared but not wired up yet.");
    }
}
