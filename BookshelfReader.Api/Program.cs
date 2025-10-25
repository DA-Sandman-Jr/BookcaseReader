using BookshelfReader.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddBookshelfReaderHost();

var app = builder.Build();

app.UseBookshelfReaderHost();

app.Run();
