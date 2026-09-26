using Microsoft.Extensions.Logging;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// Validates the settings and writes them to the file the application reads.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is applied here. Writing the file is the whole of the operation, because every part of
/// the application that cares about a setting is already watching that file: the hotkey, the
/// start-with-sign-in registration, the ranking weights and the log level all follow from the write
/// rather than from a second call made alongside it. That is what keeps the file the single source
/// of truth instead of one of two.
/// </para>
/// <para>
/// A write that fails is reported rather than thrown. A settings file on a full disk, or held open
/// by an editor, is an ordinary fact about a machine in use.
/// </para>
/// </remarks>
public sealed class UpdateSettingsHandler(
    ISettingsStore store,
    ILogger<UpdateSettingsHandler> logger) : IUpdateSettingsHandler
{
    /// <inheritdoc />
    public async Task<Result> HandleAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = command.Settings.Validate();

        if (validation.Error is { } rejection)
        {
            SettingsLog.SettingsRejected(logger, rejection.Code);
            return validation;
        }

        try
        {
            await store.SaveAsync(command.Settings, cancellationToken).ConfigureAwait(false);

            SettingsLog.SettingsSaved(logger);

            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SettingsLog.SettingsNotSaved(logger, exception);

            return Result.Failure(AppError.SettingsNotSaved(exception.Message));
        }
    }
}
