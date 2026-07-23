using System.IO;
using System.Text.Json;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Domain.Entities;
using JobFlow.Domain.Enums;
using JobFlow.Worker.Progress;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JobFlow.Worker.Handlers;

public sealed class PdfJobHandler : IJobHandler
{
    private readonly IProgressReporter _progressReporter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PdfJobHandler> _logger;
    private readonly string _outputDir;

    public PdfJobHandler(
        IProgressReporter progressReporter,
        IUnitOfWork unitOfWork,
        ILogger<PdfJobHandler> logger,
        IConfiguration configuration)
    {
        _progressReporter = progressReporter;
        _unitOfWork = unitOfWork;
        _logger = logger;
        
        var outputPath = configuration.GetValue("Worker:OutputDirectory", "output")!;
        _outputDir = Path.GetFullPath(outputPath);
    }

    public string JobType => "PdfReport";

    public async Task HandleAsync(Job job, string? payload, CancellationToken cancellationToken)
    {
        var options = ParsePayload(payload);

        _logger.LogInformation("Starting PDF report job {JobId}: title='{Title}'.", job.Id, options.Title);
        await _progressReporter.ReportProgressAsync(job.Id, 10, cancellationToken);

        var allJobs = await _unitOfWork.Jobs.GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.Status) && Enum.TryParse<JobStatus>(options.Status, true, out var status))
            allJobs = allJobs.Where(j => j.Status == status).ToList().AsReadOnly();

        _logger.LogInformation("Job {JobId}: Queried {Count} jobs for report.", job.Id, allJobs.Count);
        await _progressReporter.ReportProgressAsync(job.Id, 40, cancellationToken);

        var summary = new
        {
            Total = allJobs.Count,
            Pending = allJobs.Count(j => j.Status == JobStatus.Pending),
            Processing = allJobs.Count(j => j.Status == JobStatus.Processing),
            Completed = allJobs.Count(j => j.Status == JobStatus.Completed),
            Failed = allJobs.Count(j => j.Status == JobStatus.Failed)
        };

        Directory.CreateDirectory(_outputDir);
        var outputPath = Path.Combine(_outputDir, $"{job.Id}_report.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text(options.Title).FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
                    col.Item().PaddingTop(4).Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Item().Text("Summary").FontSize(16).Bold();
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                        });

                        AddSummaryRow(table, "Total Jobs", summary.Total.ToString());
                        AddSummaryRow(table, "Pending", summary.Pending.ToString());
                        AddSummaryRow(table, "Processing", summary.Processing.ToString());
                        AddSummaryRow(table, "Completed", summary.Completed.ToString());
                        AddSummaryRow(table, "Failed", summary.Failed.ToString());
                    });

                    col.Item().PaddingTop(20).Text("Job Details").FontSize(16).Bold();
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken3).Padding(6)
                                .Text("Name").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(6)
                                .Text("Status").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(6)
                                .Text("Priority").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(6)
                                .Text("Created (UTC)").FontColor(Colors.White).Bold();
                        });

                        var jobsToShow = allJobs.OrderByDescending(j => j.CreatedAtUtc).Take(options.MaxRows);
                        var even = false;
                        foreach (var j in jobsToShow)
                        {
                            var bg = even ? Colors.Grey.Lighten4 : Colors.White;
                            table.Cell().Background(bg).Padding(5).Text(j.Name);
                            table.Cell().Background(bg).Padding(5).Text(j.Status.ToString());
                            table.Cell().Background(bg).Padding(5).Text(j.Priority.ToString());
                            table.Cell().Background(bg).Padding(5).Text(j.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"));
                            even = !even;
                        }
                    });

                    if (allJobs.Count > options.MaxRows)
                    {
                        col.Item().PaddingTop(8)
                            .Text($"Showing {options.MaxRows} of {allJobs.Count} jobs.")
                            .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("JobFlow Report — Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(outputPath);

        _logger.LogInformation("Job {JobId}: PDF saved to {Path}.", job.Id, outputPath);
        await _progressReporter.ReportProgressAsync(job.Id, 100, cancellationToken);
    }

    private static void AddSummaryRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(4).Text(label);
        table.Cell().Padding(4).Text(value).Bold();
    }

    private static PdfReportOptions ParsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return new PdfReportOptions();

        return JsonSerializer.Deserialize<PdfReportOptions>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new PdfReportOptions();
    }

    private sealed class PdfReportOptions
    {
        public string Title { get; set; } = "JobFlow Status Report";
        public string? Status { get; set; }
        public int MaxRows { get; set; } = 50;
    }
}
