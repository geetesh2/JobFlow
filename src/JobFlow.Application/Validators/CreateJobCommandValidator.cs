using FluentValidation;
using JobFlow.Application.Commands.CreateJob;
using JobFlow.Domain.Enums;

namespace JobFlow.Application.Validators;

public sealed class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Job name is required.")
            .MaximumLength(200).WithMessage("Job name must not exceed 200 characters.");

        RuleFor(x => x.Priority)
            .Must(BeValidPriority)
            .When(x => x.Priority is not null)
            .WithMessage("Priority must be a valid value: Low, Normal, High, or Critical.");

        RuleFor(x => x.MaxRetries)
            .InclusiveBetween(0, 10).WithMessage("MaxRetries must be between 0 and 10.");

        RuleFor(x => x.Payload)
            .MaximumLength(1_000_000)
            .When(x => x.Payload is not null)
            .WithMessage("Payload must not exceed 1,000,000 characters.");
    }

    private static bool BeValidPriority(string? priority)
        => Enum.TryParse<JobPriority>(priority, true, out _);
}
