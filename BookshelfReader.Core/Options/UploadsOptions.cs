using System;
using System.Collections.Generic;
using System.Linq;

namespace BookshelfReader.Core.Options;

public sealed class UploadsOptions
{
    public static bool IsSupportedContentType(string? contentType)
    {
        return UploadContentTypeHelper.TryGetCanonicalContentType(contentType, out _);
    }

    public static bool TryGetCanonicalContentType(string? contentType, out string canonicalContentType)
    {
        return UploadContentTypeHelper.TryGetCanonicalContentType(contentType, out canonicalContentType);
    }

    public static bool TryGetImageSignature(string? contentType, out ReadOnlyMemory<byte> signature)
    {
        return UploadContentTypeHelper.TryGetImageSignature(contentType, out signature);
    }

    private HashSet<string> _allowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
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
                _allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            _allowedContentTypes = value.Count == 0
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(value
                    .Select(contentType => TryGetCanonicalContentType(contentType, out var canonical) ? canonical : null)
                    .OfType<string>(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
