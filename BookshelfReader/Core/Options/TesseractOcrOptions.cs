namespace BookshelfReader.Core.Options;

public sealed class TesseractOcrOptions
{
    public const string SectionName = "Ocr:Tesseract";
    public string DataPath { get; set; } = string.Empty;
    public string Language { get; set; } = "eng";
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}
