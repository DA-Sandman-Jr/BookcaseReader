using BookshelfReader.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddBookshelfReaderHost();

WebApplication app = builder.Build();

app.UseBookshelfReaderHost();

app.Run();
