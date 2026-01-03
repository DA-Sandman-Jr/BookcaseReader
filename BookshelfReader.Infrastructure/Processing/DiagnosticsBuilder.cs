using System.Collections.Generic;
using BookshelfReader.Core.Models;

namespace BookshelfReader.Infrastructure.Processing;

public sealed class DiagnosticsBuilder
{
    private readonly int _segmentCount;
    private readonly List<string> _notes = new();
    private long _elapsedMs;

    public DiagnosticsBuilder(int segmentCount)
    {
        _segmentCount = segmentCount;
    }

    public void AddNotes(IEnumerable<string> notes)
    {
        _notes.AddRange(notes);
    }

    public void SetElapsed(long elapsedMs)
    {
        _elapsedMs = elapsedMs;
    }

    public Diagnostics Build()
    {
        return new Diagnostics
        {
            SegmentCount = _segmentCount,
            ElapsedMs = _elapsedMs,
            Notes = _notes
        };
    }
}
