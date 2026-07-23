using FluentAssertions;
using FluentValidation.TestHelper;
using JobFlow.Application.Commands.CreateJob;
using JobFlow.Application.Validators;

namespace JobFlow.UnitTests.Application.Commands;

public class CreateJobCommandValidatorTests
{
    private readonly CreateJobCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_Should_Pass()
    {
        var command = new CreateJobCommand("Test Job", "Normal", null);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_Name_Should_Fail()
    {
        var command = new CreateJobCommand("", "Normal", null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_Exceeding_200_Characters_Should_Fail()
    {
        var longName = new string('A', 201);
        var command = new CreateJobCommand(longName, "Normal", null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void MaxRetries_Below_Zero_Should_Fail()
    {
        var command = new CreateJobCommand("Test Job", "Normal", null, MaxRetries: -1);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MaxRetries);
    }

    [Fact]
    public void MaxRetries_Above_Ten_Should_Fail()
    {
        var command = new CreateJobCommand("Test Job", "Normal", null, MaxRetries: 11);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MaxRetries);
    }

    [Fact]
    public void Invalid_Priority_Should_Fail()
    {
        var command = new CreateJobCommand("Test Job", "Invalid", null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Priority);
    }

    [Fact]
    public void Null_Priority_Should_Pass()
    {
        var command = new CreateJobCommand("Test Job", null, null);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }
}
