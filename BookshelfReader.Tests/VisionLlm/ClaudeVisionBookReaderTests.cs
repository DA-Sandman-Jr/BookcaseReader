using System.Net;
using System.Text;
using System.Text.Json;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using BookshelfReader.Infrastructure.VisionLlm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using RichardSzalay.MockHttp;
using Xunit;

namespace BookshelfReader.Tests.VisionLlm;

public class ClaudeVisionBookReaderTests
{
    private static readonly byte[] TestImageBytes = CreateJpegBytes();

    [Fact]
    public async Task ReadAsync_OnSuccess_ParsesBooksFromStructuredOutput()
    {
        const string responseJson = """
            {
              "content": [
                { "type": "text", "text": "{\"books\":[{\"title\":\"Dune\",\"author\":\"Frank Herbert\",\"confidence\":0.95},{\"title\":\"Foundation\",\"author\":\"\",\"confidence\":0.6}]}" }
              ],
              "stop_reason": "end_turn"
            }
            """;

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            .Respond("application/json", responseJson);

        ClaudeVisionBookReader reader = CreateReader(mockHttp);

        VisionReadResult result = await reader.ReadAsync(TestImageBytes);

        result.Books.Should().HaveCount(2);
        result.Books[0].Should().Be(new VisionBookEntry("Dune", "Frank Herbert", 0.95));
        result.Books[1].Should().Be(new VisionBookEntry("Foundation", string.Empty, 0.6));
        result.Notes.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_SendsExpectedRequestShape()
    {
        const string responseJson = """{"content":[{"type":"text","text":"{\"books\":[]}"}],"stop_reason":"end_turn"}""";

        var mockHttp = new MockHttpMessageHandler();
        string? capturedBody = null;

        mockHttp.When(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            .Respond(async request =>
            {
                capturedBody = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });

        ClaudeVisionBookReader reader = CreateReader(mockHttp, options =>
        {
            options.Model = "claude-haiku-4-5";
            options.MaxTokens = 1234;
        });

        await reader.ReadAsync(TestImageBytes);

        capturedBody.Should().NotBeNull();
        using JsonDocument document = JsonDocument.Parse(capturedBody!);
        JsonElement root = document.RootElement;

        root.GetProperty("model").GetString().Should().Be("claude-haiku-4-5");
        root.GetProperty("max_tokens").GetInt32().Should().Be(1234);

        JsonElement message = root.GetProperty("messages")[0];
        message.GetProperty("role").GetString().Should().Be("user");

        JsonElement imageBlock = message.GetProperty("content")[0];
        imageBlock.GetProperty("type").GetString().Should().Be("image");
        imageBlock.GetProperty("source").GetProperty("type").GetString().Should().Be("base64");
        imageBlock.GetProperty("source").GetProperty("media_type").GetString().Should().Be("image/jpeg");
        imageBlock.GetProperty("source").GetProperty("data").GetString().Should().NotBeNullOrEmpty();

        JsonElement textBlock = message.GetProperty("content")[1];
        textBlock.GetProperty("type").GetString().Should().Be("text");
        textBlock.GetProperty("text").GetString().Should().NotBeNullOrEmpty();

        JsonElement schema = root.GetProperty("output_config").GetProperty("format");
        schema.GetProperty("type").GetString().Should().Be("json_schema");
        schema.GetProperty("schema").GetProperty("required")[0].GetString().Should().Be("books");
    }

    [Fact]
    public async Task ReadAsync_WhenStructuredOutputIsUnparseable_ReturnsNoBooksWithNote()
    {
        const string responseJson = """
            {
              "content": [ { "type": "text", "text": "not json" } ],
              "stop_reason": "end_turn"
            }
            """;

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            .Respond("application/json", responseJson);

        ClaudeVisionBookReader reader = CreateReader(mockHttp);

        VisionReadResult result = await reader.ReadAsync(TestImageBytes);

        result.Books.Should().BeEmpty();
        result.Notes.Should().ContainSingle(note => note.Contains("could not be parsed"));
    }

    [Fact]
    public async Task ReadAsync_WhenStopReasonIsMaxTokens_KeepsParsedBooksAndAddsTruncationNote()
    {
        const string responseJson = """
            {
              "content": [ { "type": "text", "text": "{\"books\":[{\"title\":\"Dune\",\"author\":\"Frank Herbert\",\"confidence\":0.9}]}" } ],
              "stop_reason": "max_tokens"
            }
            """;

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            .Respond("application/json", responseJson);

        ClaudeVisionBookReader reader = CreateReader(mockHttp);

        VisionReadResult result = await reader.ReadAsync(TestImageBytes);

        result.Books.Should().ContainSingle();
        result.Notes.Should().ContainSingle(note => note.Contains("truncated"));
    }

    [Fact]
    public async Task ReadAsync_OnUnauthorized_ThrowsVisionBookReaderExceptionDescribingApiKeyRejection()
    {
        const string responseJson = """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""";

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            .Respond(HttpStatusCode.Unauthorized, "application/json", responseJson);

        ClaudeVisionBookReader reader = CreateReader(mockHttp);

        Func<Task> act = () => reader.ReadAsync(TestImageBytes);

        (await act.Should().ThrowAsync<VisionBookReaderException>())
            .WithMessage("*rejected the configured API key (401)*invalid x-api-key*");
    }

    [Fact]
    public async Task ReadAsync_OnTooManyRequests_ThrowsVisionBookReaderExceptionDescribingRateLimit()
    {
        const string responseJson = """{"type":"error","error":{"type":"rate_limit_error","message":"rate limited"}}""";

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            .Respond(HttpStatusCode.TooManyRequests, "application/json", responseJson);

        ClaudeVisionBookReader reader = CreateReader(mockHttp);

        Func<Task> act = () => reader.ReadAsync(TestImageBytes);

        (await act.Should().ThrowAsync<VisionBookReaderException>())
            .WithMessage("*rate limiting requests (429)*rate limited*");
    }

    [Fact]
    public async Task ReadAsync_OnServerError_ThrowsVisionBookReaderExceptionDescribingServerError()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            .Respond(HttpStatusCode.InternalServerError, "application/json", "{}");

        ClaudeVisionBookReader reader = CreateReader(mockHttp);

        Func<Task> act = () => reader.ReadAsync(TestImageBytes);

        (await act.Should().ThrowAsync<VisionBookReaderException>())
            .WithMessage("*server error (500)*");
    }

    private static ClaudeVisionBookReader CreateReader(MockHttpMessageHandler mockHttp, Action<ClaudeVisionOptions>? configure = null)
    {
        var options = new ClaudeVisionOptions
        {
            ApiKey = "test-api-key"
        };
        configure?.Invoke(options);

        HttpClient httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://api.anthropic.com/");

        return new ClaudeVisionBookReader(httpClient, Microsoft.Extensions.Options.Options.Create(options), NullLogger<ClaudeVisionBookReader>.Instance);
    }

    private static byte[] CreateJpegBytes()
    {
        using var mat = new Mat(8, 8, MatType.CV_8UC3, new Scalar(255, 255, 255));
        Cv2.ImEncode(".jpg", mat, out byte[] bytes);
        return bytes;
    }
}
