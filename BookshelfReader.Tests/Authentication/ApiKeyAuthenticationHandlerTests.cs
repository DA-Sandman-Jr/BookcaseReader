using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using BookshelfReader.DependencyInjection.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BookshelfReader.Tests.Authentication;

public class ApiKeyAuthenticationHandlerTests
{
    [Fact]
    public async Task AuthenticateAsync_Fails_WhenHeaderMissing()
    {
        var options = CreateOptions();
        var context = new DefaultHttpContext();
        var handler = CreateHandler(options, context);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_Fails_WhenHeaderEmpty()
    {
        var options = CreateOptions();
        var context = new DefaultHttpContext();
        context.Request.Headers[options.HeaderName] = string.Empty;
        var handler = CreateHandler(options, context);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_Succeeds_WithValidKey()
    {
        const string expectedKey = "valid-secret";
        var options = CreateOptions(validKey: expectedKey);
        var context = new DefaultHttpContext();
        context.Request.Headers[options.HeaderName] = expectedKey;
        var handler = CreateHandler(options, context);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.FindFirst(ApiKeyAuthenticationDefaults.ApiKeyClaimType)!.Value
            .Should().Be(ApiKeyValidator.CreateKeyIdentifier(expectedKey));
    }

    private static ApiKeyAuthenticationHandler CreateHandler(ApiKeyAuthenticationOptions options, HttpContext context)
    {
        var optionsMonitor = new StaticOptionsMonitor<ApiKeyAuthenticationOptions>(options);
        var handler = new ApiKeyAuthenticationHandler(optionsMonitor, NullLoggerFactory.Instance, UrlEncoder.Default, new SystemClock());
        var scheme = new AuthenticationScheme(ApiKeyAuthenticationDefaults.AuthenticationScheme,
            ApiKeyAuthenticationDefaults.AuthenticationScheme, typeof(ApiKeyAuthenticationHandler));

        handler.InitializeAsync(scheme, context).GetAwaiter().GetResult();
        return handler;
    }

    private static ApiKeyAuthenticationOptions CreateOptions(string validKey = "valid")
    {
        return new ApiKeyAuthenticationOptions
        {
            RequireApiKey = true,
            ValidKeys = { validKey }
        };
    }

    private sealed class StaticOptionsMonitor<TOptions> : IOptionsMonitor<TOptions> where TOptions : class
    {
        public StaticOptionsMonitor(TOptions options)
        {
            CurrentValue = options;
        }

        public TOptions CurrentValue { get; }

        public TOptions Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable OnChange(Action<TOptions, string?> listener)
        {
            return NullDisposable.Instance;
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
