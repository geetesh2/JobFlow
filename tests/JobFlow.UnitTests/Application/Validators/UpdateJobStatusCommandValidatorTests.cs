using FluentAssertions;
using FluentValidation.TestHelper;
using JobFlow.Application.Commands.UpdateJobStatus;
using JobFlow.Application.Validators;

namespace JobFlow.UnitTests.Application.Validators;

public class UpdateJobStatusCommandValidatorTests
{
    private readonly UpdateJobStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenJobIdIsEmpty()
    {
        var command = new UpdateJobStatusCommand(Guid.Empty, "Processing");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.JobId);
    }

    [Fact]
    public void Validate_ShouldFail_WhenStatusIsEmpty()
    {
        var command = new UpdateJobStatusCommand(Guid.NewGuid(), "");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Validate_ShouldFail_WhenStatusIsInvalid()
    {
        var command = new UpdateJobStatusCommand(Guid.NewGuid(), "NotAStatus");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var command = new UpdateJobStatusCommand(Guid.NewGuid(), "Processing");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Processing")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("pending")]
    [InlineData("COMPLETED")]
    public void Validate_ShouldPass_ForAllValidStatuses(string status)
    {
        var command = new UpdateJobStatusCommand(Guid.NewGuid(), status);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
