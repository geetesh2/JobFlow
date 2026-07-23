using FluentValidation;
using JobFlow.Application.Commands.UpdateJobStatus;
using JobFlow.Domain.Enums;

namespace JobFlow.Application.Validators;

public sealed class UpdateJobStatusCommandValidator : AbstractValidator<UpdateJobStatusCommand>
{
    private static readonly string[] ValidStatuses =
        Enum.GetNames<JobStatus>().Select(s => s.ToLowerInvariant()).ToArray();

    public UpdateJobStatusCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(status => ValidStatuses.Contains(status.ToLowerInvariant()))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<JobStatus>())}.");
    }
}
