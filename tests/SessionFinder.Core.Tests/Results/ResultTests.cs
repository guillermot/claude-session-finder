using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_ASuccessfulOutcome_CarriesNoError()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_AFailedOutcome_CarriesTheReason()
    {
        var result = Result.Failure(AppError.FolderUnknown);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AppError.FolderUnknown);
    }

    [Fact]
    public void Match_ASuccessfulOutcome_TakesTheSuccessBranch()
    {
        var branch = Result.Success().Match(() => "worked", error => error.Code);

        branch.Should().Be("worked");
    }

    [Fact]
    public void Match_AFailedOutcome_TakesTheFailureBranch()
    {
        var branch = Result.Failure(AppError.EditorNotFound).Match(() => "worked", error => error.Code);

        branch.Should().Be(nameof(AppError.EditorNotFound));
    }

    [Fact]
    public void Success_AValueResult_ExposesTheValue()
    {
        var result = Result<string>.Success("a value");

        result.Value.Should().Be("a value");
    }

    [Fact]
    public void Value_AFailedValueResult_ThrowsRatherThanReturningADefault()
    {
        var result = Result<string>.Failure(AppError.TerminalNotFound);

        var reading = () => result.Value;

        reading.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Match_AFailedValueResult_TakesTheFailureBranch()
    {
        var branch = Result<string>.Failure(AppError.TerminalNotFound)
            .Match(value => value, error => error.Code);

        branch.Should().Be(nameof(AppError.TerminalNotFound));
    }
}
