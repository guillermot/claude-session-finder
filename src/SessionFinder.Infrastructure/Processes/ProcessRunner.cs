using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using SessionFinder.Core.Features.SessionActions;

namespace SessionFinder.Infrastructure.Processes;

/// <summary>
/// Runs a program with every stream redirected, a deadline, and a kill when the deadline passes.
/// </summary>
/// <remarks>
/// <para>
/// Both output streams are drained while the input is still being written. A child that fills its
/// output pipe blocks until somebody reads it, and a parent that only reads once the input is
/// written then waits for a child that is waiting for it.
/// </para>
/// <para>
/// The inherited environment is scrubbed the same way the shell launchers scrub it, for the same
/// reason: a variable inherited from an editor's terminal can change what the program is.
/// </para>
/// </remarks>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var process = new Process { StartInfo = BuildStartInfo(request) };

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return ProcessOutcome.NotStarted(exception.Message);
        }

        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.Timeout);

        try
        {
            await WriteInputAsync(process, request.StandardInput, deadline.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            cancellationToken.ThrowIfCancellationRequested();

            return ProcessOutcome.Expired;
        }

        return new ProcessOutcome
        {
            ExitCode = process.ExitCode,
            StandardOutput = await output.ConfigureAwait(false),
            StandardError = await error.ConfigureAwait(false),
        };
    }

    private static ProcessStartInfo BuildStartInfo(ProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        InheritedEnvironment.Scrub(startInfo.Environment);

        foreach (var (name, value) in request.Environment)
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }

    /// <summary>
    /// Writes the input and closes the stream, which is how the child learns there is no more. A
    /// child that exits without reading all of it closes the pipe under the write; what it printed
    /// is still the answer, so that is not a failure.
    /// </summary>
    private static async Task WriteInputAsync(Process process, string? input, CancellationToken cancellationToken)
    {
        try
        {
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            return;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (Win32Exception)
        {
            return;
        }
    }
}
