using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Registers the launcher to start when the user signs in, through a <c>launchd</c> agent.
/// </summary>
/// <remarks>
/// <para>
/// The agent runs <c>open -a &lt;bundle&gt;</c> rather than the executable inside the bundle. Started
/// directly, the process is not a bundled application as far as the window server is concerned: it
/// gets a Dock icon the bundle's own property list asks it not to have, and it loses the identity
/// that notifications and permissions are granted against.
/// </para>
/// <para>
/// An agent whose program is some other bundle reads as not enabled, which is what makes the
/// registration repair itself after the application is moved: the reconciler sees a disagreement
/// with the setting and writes the file again, this time pointing at where the application now is.
/// The same reasoning is in the Windows implementation, for the same reason.
/// </para>
/// <para>
/// Writing the file is enough for the next sign-in. <c>launchctl</c> is run as well so that
/// enabling the setting takes effect without one, and a failure from it is not treated as a failure
/// of the whole operation — the file on disk is what sign-in reads, and it is already correct.
/// </para>
/// </remarks>
internal sealed class LaunchAgentAutostart : IAutostart
{
    /// <summary>Reverse-DNS label the agent is registered under.</summary>
    public const string AgentLabel = "com.gtama.claudesessionfinder";

    private const string LaunchAgentsFolder = "Library/LaunchAgents";
    private const string OpenExecutable = "/usr/bin/open";
    private const string ApplicationFlag = "-a";
    private const string LaunchControlExecutable = "/bin/launchctl";
    private const string NoBundleReason =
        "the application is not running from an .app bundle, so there is nothing to register";

    private static readonly TimeSpan LaunchControlTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public Result<bool> ReadIsEnabled()
    {
        try
        {
            if (ApplicationBundle.Path() is not { } bundle)
            {
                return Result<bool>.Success(false);
            }

            return Result<bool>.Success(RegisteredBundle() == bundle);
        }
        catch (IOException exception)
        {
            return Result<bool>.Failure(AppError.AutostartUnavailable(exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result<bool>.Failure(AppError.AutostartUnavailable(exception.Message));
        }
    }

    /// <inheritdoc />
    public Result Apply(bool shouldStartWithSession)
    {
        try
        {
            return shouldStartWithSession ? Enable() : Disable();
        }
        catch (IOException exception)
        {
            return Result.Failure(AppError.AutostartUnavailable(exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Failure(AppError.AutostartUnavailable(exception.Message));
        }
    }

    private static Result Enable()
    {
        if (ApplicationBundle.Path() is not { } bundle)
        {
            return Result.Failure(AppError.AutostartUnavailable(NoBundleReason));
        }

        var path = AgentFilePath();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Booted out first so that a replaced file is actually reloaded rather than shadowed by the
        // agent launchd already has in memory from the previous path.
        RunLaunchControl("bootout");
        BuildAgent(bundle).Save(path);
        RunLaunchControl("bootstrap");

        return Result.Success();
    }

    private static Result Disable()
    {
        var path = AgentFilePath();

        if (!File.Exists(path))
        {
            return Result.Success();
        }

        RunLaunchControl("bootout");
        File.Delete(path);

        return Result.Success();
    }

    /// <summary>
    /// Reads the bundle the installed agent points at, or <see langword="null"/> when there is no
    /// agent or its contents are not the shape this application writes.
    /// </summary>
    private static string? RegisteredBundle()
    {
        var path = AgentFilePath();

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var arguments = XDocument.Load(path)
                .Descendants("array")
                .FirstOrDefault()
                ?.Elements("string")
                .Select(element => element.Value)
                .ToArray();

            return arguments is [OpenExecutable, ApplicationFlag, var bundle] ? bundle : null;
        }
        catch (System.Xml.XmlException)
        {
            // Somebody else's malformed file. Reporting it as "not registered" makes the reconciler
            // overwrite it with a correct one, which is the outcome the user wanted anyway.
            return null;
        }
    }

    private static XDocument BuildAgent(string bundle) => new(
        new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN",
            "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
        new XElement(
            "plist",
            new XAttribute("version", "1.0"),
            new XElement(
                "dict",
                new XElement("key", "Label"),
                new XElement("string", AgentLabel),
                new XElement("key", "ProgramArguments"),
                new XElement(
                    "array",
                    new XElement("string", OpenExecutable),
                    new XElement("string", ApplicationFlag),
                    new XElement("string", bundle)),
                new XElement("key", "RunAtLoad"),
                new XElement("true"))));

    /// <summary>
    /// Asks launchd to pick the change up now. Every failure is swallowed: the file is what a
    /// sign-in reads, it has already been written, and an agent that was not loaded or not present
    /// to unload is the ordinary case rather than a fault.
    /// </summary>
    private static void RunLaunchControl(string verb)
    {
        var domain = $"gui/{LibC.GetUserId()}";

        var startInfo = new ProcessStartInfo
        {
            FileName = LaunchControlExecutable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        startInfo.ArgumentList.Add(verb);

        if (verb == "bootstrap")
        {
            startInfo.ArgumentList.Add(domain);
            startInfo.ArgumentList.Add(AgentFilePath());
        }
        else
        {
            startInfo.ArgumentList.Add($"{domain}/{AgentLabel}");
        }

        try
        {
            using var process = Process.Start(startInfo);

            process?.WaitForExit(LaunchControlTimeout);
        }
        catch (Win32Exception)
        {
            // launchctl is part of the system; if it cannot be run, the file still stands.
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string AgentFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        LaunchAgentsFolder,
        $"{AgentLabel}.plist");
}
