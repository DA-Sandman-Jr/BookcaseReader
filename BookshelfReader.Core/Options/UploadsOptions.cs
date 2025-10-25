using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BookshelfReader.Core.Options;

public sealed class UploadsOptions
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly IReadOnlyDictionary<string, string> CanonicalContentTypeMap =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Comparer)
        {
            ["image/jpeg"] = "image/jpeg",
            ["image/jpg"] = "image/jpeg",
            ["image/pjpeg"] = "image/jpeg",
            ["image/png"] = "image/png"
        });

    private static readonly IReadOnlyDictionary<string, ReadOnlyMemory<byte>> SupportedSignatures =
        new ReadOnlyDictionary<string, ReadOnlyMemory<byte>>(new Dictionary<string, ReadOnlyMemory<byte>>(Comparer)
        {
            ["image/jpeg"] = JpegSignature,
            ["image/png"] = PngSignature
        });

    public static IReadOnlyDictionary<string, ReadOnlyMemory<byte>> SupportedImageSignatures => SupportedSignatures;

    public static bool IsSupportedContentType(string? contentType)
    {
        return TryGetCanonicalContentType(contentType, out _);
    }

    public static bool TryGetCanonicalContentType(string? contentType, out string canonicalContentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            canonicalContentType = string.Empty;
            return false;
        }

        if (CanonicalContentTypeMap.TryGetValue(contentType, out var canonical))
        {
            canonicalContentType = canonical;
            return true;
        }

        canonicalContentType = string.Empty;
        return false;
    }

    public static bool TryGetImageSignature(string? contentType, out ReadOnlyMemory<byte> signature)
    {
        signature = default;
        return TryGetCanonicalContentType(contentType, out var canonical)
            && SupportedImageSignatures.TryGetValue(canonical, out signature);
    }

    private HashSet<string> _allowedContentTypes = new(Comparer)
    {
        "image/jpeg",
        "image/png"
    };

    public const string SectionName = "Uploads";

    public int MaxBytes { get; set; } = 10 * 1024 * 1024;

    public HashSet<string> AllowedContentTypes
    {
        get => _allowedContentTypes;
        set
        {
            if (value is null)
            {
                _allowedContentTypes = new HashSet<string>(Comparer);
                return;
            }

            _allowedContentTypes = value.Count == 0
                ? new HashSet<string>(Comparer)
                : new HashSet<string>(value
                    .Select(contentType => TryGetCanonicalContentType(contentType, out var canonical) ? canonical : null)
                    .OfType<string>(), Comparer);
        }
    }
}
