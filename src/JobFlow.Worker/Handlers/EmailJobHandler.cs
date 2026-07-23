using JobFlow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace JobFlow.Worker.Handlers;

public sealed class EmailJobHandler : IJobHandler
{
    private readonly ILogger<EmailJobHandler> _logger;

    public EmailJobHandler(ILogger<EmailJobHandler> logger)
    {
        _logger = logger;
    }

    public string JobType => "Email";

    public async Task HandleAsync(Job job, string? payload, CancellationToken cancellationToken)
    {
        var recipient = payload ?? "default@test.com";
        _logger.LogInformation("Sending email for job {JobId} to {Recipient}.", job.Id, recipient);

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        _logger.LogInformation("Email sent successfully for job {JobId} to {Recipient}.", job.Id, recipient);
    }
}
