using BookshelfReader.Core.Models.External;
using BookshelfReader.Infrastructure.Lookup;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RichardSzalay.MockHttp;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Linq;

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

        var results = await service.LookupAsync("Test");

        results.Should().HaveCount(2);
        results.First().Title.Should().Be("Book A");
        results.First().Author.Should().Be("Author A");
        results.First().PublishYear.Should().Be(2020);
        results.First().Isbn.Should().Be("123");
        results.First().CoverUrl.Should().Be("https://covers.openlibrary.org/b/id/42-M.jpg");
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

        var results = await service.LookupAsync("Test");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_WithEmptyQuery_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = handler.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://openlibrary.org/");

        var service = new OpenLibraryLookupService(httpClient, NullLogger<OpenLibraryLookupService>.Instance);

        var results = await service.LookupAsync(string.Empty);
        results.Should().BeEmpty();
    }
}
