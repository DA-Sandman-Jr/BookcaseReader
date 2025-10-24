using BookshelfReader.Api.Endpoints;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Options;
using BookshelfReader.Infrastructure.Genres;
using BookshelfReader.Infrastructure.Lookup;
using BookshelfReader.Infrastructure.Ocr;
using BookshelfReader.Infrastructure.Parsing;
using BookshelfReader.Infrastructure.Processing;
using BookshelfReader.Infrastructure.Segmentation;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.Services.Configure<UploadsOptions>(builder.Configuration.GetSection(UploadsOptions.SectionName));
builder.Services.Configure<TesseractOcrOptions>(builder.Configuration.GetSection(TesseractOcrOptions.SectionName));
builder.Services.Configure<SegmentationOptions>(builder.Configuration.GetSection(SegmentationOptions.SectionName));
builder.Services.Configure<ParsingOptions>(builder.Configuration.GetSection(ParsingOptions.SectionName));

builder.Services.AddSingleton<IBookSegmentationService, OpenCvBookSegmentationService>();
builder.Services.AddSingleton<IOcrService, TesseractOcrService>();
builder.Services.AddSingleton<IBookParsingService, BookParsingService>();
builder.Services.AddSingleton<IGenreClassifier, KeywordGenreClassifier>();
builder.Services.AddSingleton<IBookshelfProcessingService, BookshelfProcessingService>();

builder.Services.AddHttpClient<IBookLookupService, OpenLibraryLookupService>(client =>
{
    var baseUrl = builder.Configuration["OpenLibrary:BaseUrl"] ?? "https://openlibrary.org/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapApiEndpoints();

app.Run();
