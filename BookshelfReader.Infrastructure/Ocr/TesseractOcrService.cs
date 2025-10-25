using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private readonly ConcurrentBag<TesseractEngine> _enginePool = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly string _dataPath;
    private readonly string _language;
    private bool _disposed;

    public TesseractOcrService(IOptions<TesseractOcrOptions> options, ILogger<TesseractOcrService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _dataPath = string.IsNullOrWhiteSpace(_options.DataPath)
            ? Path.Combine(AppContext.BaseDirectory, "tessdata")
            : _options.DataPath;
        _language = _options.Language;

        if (!Directory.Exists(_dataPath))
        {
            _logger.LogWarning("Tesseract data path '{Path}' not found. OCR results may be degraded.", _dataPath);
        }

        var parallelism = Math.Max(1, _options.MaxDegreeOfParallelism);
        _semaphore = new SemaphoreSlim(parallelism, parallelism);

        for (var i = 0; i < parallelism; i++)
        {
            _enginePool.Add(CreateEngine());
        }
    }

    public async Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (imageData is null || imageData.Length == 0)
        {
            return new OcrResult();
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        TesseractEngine? engine = null;
        try
        {
            if (!_enginePool.TryTake(out engine))
            {
                engine = CreateEngine();
            }

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
                using var page = engine.Process(oriented);
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
            if (engine is not null)
            {
                _enginePool.Add(engine);
            }
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        while (_enginePool.TryTake(out var engine))
        {
            engine.Dispose();
        }
        _semaphore.Dispose();
        _disposed = true;
    }

    private TesseractEngine CreateEngine()
    {
        var engine = new TesseractEngine(_dataPath, _language, EngineMode.Default);
        engine.SetVariable("tessedit_do_invert", "0");
        engine.DefaultPageSegMode = PageSegMode.Auto;
        return engine;
    }
}
