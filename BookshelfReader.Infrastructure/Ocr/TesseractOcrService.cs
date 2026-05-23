using System.Collections.Concurrent;
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
    private readonly int _maxEngines;
    private int _engineCount;
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

        int parallelism = Math.Max(1, _options.MaxDegreeOfParallelism);
        _maxEngines = parallelism;
        _semaphore = new SemaphoreSlim(parallelism, parallelism);

        for (int i = 0; i < parallelism; i++)
        {
            _enginePool.Add(CreateEngine());
            _engineCount++;
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
        bool returnToPool = true;
        try
        {
            if (!_enginePool.TryTake(out engine))
            {
                // The semaphore should guarantee the pool always has an engine available.
                // If we reach this path something went wrong; cap growth at the configured max.
                int newCount = Interlocked.Increment(ref _engineCount);
                if (newCount <= _maxEngines)
                {
                    engine = CreateEngine();
                }
                else
                {
                    Interlocked.Decrement(ref _engineCount);
                    _logger.LogWarning("OCR engine pool exhausted unexpectedly at concurrency limit {Max}; creating temporary engine", _maxEngines);
                    engine = CreateEngine();
                    returnToPool = false;
                }
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

            foreach ((int Angle, Func<Pix> Factory) orientation in orientations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Pix oriented = orientation.Factory();
                using Page page = engine.Process(oriented);
                string text = page.GetText() ?? string.Empty;
                float confidence = page.GetMeanConfidence();
                attempts.Add(text);
                confidences.Add(confidence);

                if (confidence > bestConfidence && !string.IsNullOrWhiteSpace(text))
                {
                    bestConfidence = confidence;
                    bestText = text;
                }
            }

            double averageConfidence = confidences.Count == 0 ? 0 : confidences.Average();
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
                if (returnToPool)
                {
                    _enginePool.Add(engine);
                }
                else
                {
                    engine.Dispose();
                }
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

        while (_enginePool.TryTake(out TesseractEngine? engine))
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
