using System.IO;
using System.Text.Json;
using JobFlow.Domain.Entities;
using JobFlow.Worker.Progress;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace JobFlow.Worker.Handlers;

public sealed class ImageResizeJobHandler : IJobHandler
{
    private readonly IProgressReporter _progressReporter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageResizeJobHandler> _logger;
    private readonly string _outputDir;

    public ImageResizeJobHandler(
        IProgressReporter progressReporter,
        IHttpClientFactory httpClientFactory,
        ILogger<ImageResizeJobHandler> logger,
        IConfiguration configuration)
    {
        _progressReporter = progressReporter;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        
        var outputPath = configuration.GetValue("Worker:OutputDirectory", "output")!;
        _outputDir = Path.GetFullPath(outputPath);
    }

    public string JobType => "ImageResize";

    public async Task HandleAsync(Job job, string? payload, CancellationToken cancellationToken)
    {
        var options = ParsePayload(payload);

        _logger.LogInformation("Starting image resize job {JobId}: {Url} → {Width}x{Height}.",
            job.Id, options.Url, options.Width, options.Height);

        await _progressReporter.ReportProgressAsync(job.Id, 10, cancellationToken);

        var client = _httpClientFactory.CreateClient("ImageDownload");
        using var response = await client.GetAsync(options.Url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        _logger.LogInformation("Job {JobId}: Downloaded image ({Bytes} bytes).", job.Id, response.Content.Headers.ContentLength);

        await _progressReporter.ReportProgressAsync(job.Id, 40, cancellationToken);

        using var image = await Image.LoadAsync(sourceStream, cancellationToken);
        _logger.LogInformation("Job {JobId}: Original size {W}x{H}.", job.Id, image.Width, image.Height);

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(options.Width, options.Height),
            Mode = ResizeMode.Max
        }));

        _logger.LogInformation("Job {JobId}: Resized to {W}x{H}.", job.Id, image.Width, image.Height);
        await _progressReporter.ReportProgressAsync(job.Id, 75, cancellationToken);

        Directory.CreateDirectory(_outputDir);
        var outputPath = Path.Combine(_outputDir, $"{job.Id}_resized.{options.Format}");

        await image.SaveAsync(outputPath, cancellationToken);

        _logger.LogInformation("Job {JobId}: Saved to {Path}.", job.Id, outputPath);
        await _progressReporter.ReportProgressAsync(job.Id, 100, cancellationToken);
    }

    private static ImageResizeOptions ParsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload is required. Expected: {\"url\": \"...\", \"width\": 800, \"height\": 600}");

        var options = JsonSerializer.Deserialize<ImageResizeOptions>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (options is null || string.IsNullOrWhiteSpace(options.Url))
            throw new ArgumentException("Payload must contain a valid 'url' field.");

        return options;
    }

    private sealed class ImageResizeOptions
    {
        public string Url { get; set; } = string.Empty;
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public string Format { get; set; } = "png";
    }
}
