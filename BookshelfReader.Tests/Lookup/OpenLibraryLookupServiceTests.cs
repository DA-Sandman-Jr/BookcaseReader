using System.Text.Json;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Models.External;
using BookshelfReader.Infrastructure.Lookup;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RichardSzalay.MockHttp;
using Xunit;

namespace BookshelfReader.Tests.Lookup;

public class OpenLibraryLookupServiceTests
{
    [Fact]
    public async Task LookupAsync_MapsTopResults()
    {
        var handler = new MockHttpMessageHandler();
        var response = new OpenLibrarySearchResult
        {
            Docs = new List<OpenLibraryDoc>
            {
                new() { Title = "Book A", AuthorName = new() { "Author A" }, FirstPublishYear = 2020, Isbn = new() { "123" }, CoverId = 42 },
                new() { Title = "Book B", AuthorName = new() { "Author B" }, FirstPublishYear = 2018, Isbn = new() { "321" } }
            }
        };

        handler.When(HttpMethod.Get, "https://openlibrary.org/search.json*")
            .Respond("application/json", JsonSerializer.Serialize(response));

        var httpClient = handler.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://openlibrary.org/");
        var service = new OpenLibraryLookupService(httpClient, NullLogger<OpenLibraryLookupService>.Instance);

        BookLookupResult result = await service.LookupAsync("Test");
        var books = result.Books.ToList();

        result.ErrorMessage.Should().BeNull();
        books.Should().HaveCount(2);
        books[0].Title.Should().Be("Book A");
        books[0].Author.Should().Be("Author A");
        books[0].PublishYear.Should().Be(2020);
        books[0].Isbn.Should().Be("123");
        books[0].CoverUrl.Should().Be("https://covers.openlibrary.org/b/id/42-M.jpg");
    }

    [Fact]
    public async Task LookupAsync_WhenRequestFails_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Get, "https://openlibrary.org/*")
            .Throw(new HttpRequestException("boom"));

        var httpClient = handler.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://openlibrary.org/");

        var service = new OpenLibraryLookupService(httpClient, NullLogger<OpenLibraryLookupService>.Instance);

        BookLookupResult result = await service.LookupAsync("Test");
        result.Books.ToList().Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_WithEmptyQuery_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = handler.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://openlibrary.org/");

        var service = new OpenLibraryLookupService(httpClient, NullLogger<OpenLibraryLookupService>.Instance);

        BookLookupResult result = await service.LookupAsync(string.Empty);
        result.Books.ToList().Should().BeEmpty();
    }
}
