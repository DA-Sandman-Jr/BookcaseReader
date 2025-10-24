using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace BookshelfReader.Infrastructure.Ocr;

public sealed class TesseractOcrService : IOcrService, IDisposable
{
    private readonly TesseractOcrOptions _options;
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly TesseractEngine _engine;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public TesseractOcrService(IOptions<TesseractOcrOptions> options, ILogger<TesseractOcrService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var dataPath = string.IsNullOrWhiteSpace(_options.DataPath)
            ? Path.Combine(AppContext.BaseDirectory, "tessdata")
            : _options.DataPath;

        if (!Directory.Exists(dataPath))
        {
            _logger.LogWarning("Tesseract data path '{Path}' not found. OCR results may be degraded.", dataPath);
        }

        _engine = new TesseractEngine(dataPath, _options.Language, EngineMode.Default);
        _engine.SetVariable("tessedit_do_invert", "0");
        _engine.DefaultPageSegMode = PageSegMode.Auto;

        var parallelism = Math.Max(1, _options.MaxDegreeOfParallelism);
        _semaphore = new SemaphoreSlim(parallelism, parallelism);
    }

    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (imageData is null || imageData.Length == 0)
        {
            return new OcrResult();
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var pix = Pix.LoadFromMemory(imageData);
            var orientations = new List<(int Angle, Func<Pix> Factory)>
            {
                (0, pix.Clone),
                (90, () => pix.Rotate90(1)),
                (270, () => pix.Rotate90(3))
            };

            var attempts = new List<string>();
            var confidences = new List<double>();
            string bestText = string.Empty;
            double bestConfidence = 0;

            foreach (var orientation in orientations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var oriented = orientation.Factory();
                using var page = _engine.Process(oriented);
                var text = page.GetText() ?? string.Empty;
                var confidence = page.GetMeanConfidence();
                attempts.Add(text);
                confidences.Add(confidence);

                if (confidence > bestConfidence && !string.IsNullOrWhiteSpace(text))
                {
                    bestConfidence = confidence;
                    bestText = text;
                }
            }

            var averageConfidence = confidences.Count == 0 ? 0 : confidences.Average();
            return new OcrResult
            {
                Text = bestText.Trim(),
                Confidence = averageConfidence,
                Attempts = attempts.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tesseract OCR failed");
            return new OcrResult();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _engine.Dispose();
        _semaphore.Dispose();
        _disposed = true;
    }
}
