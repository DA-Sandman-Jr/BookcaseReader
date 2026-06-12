using System.Globalization;
using System.Threading.RateLimiting;
using BookshelfReader.Core.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookshelfReader.Api.RateLimiting;

public static class BookshelfReaderRateLimitingExtensions
{
    public const string ParsePolicyName = "bookshelf-reader-parse";

    public static IServiceCollection AddBookshelfReaderRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ParseRateLimitOptions>()
            .Bind(configuration.GetSection(ParseRateLimitOptions.SectionName))
            .Validate(options => options.PermitLimit >= 1,
                "RateLimiting:Parse:PermitLimit must be at least 1.")
            .Validate(options => options.WindowSeconds is >= 1 and <= 3600,
                "RateLimiting:Parse:WindowSeconds must be between 1 and 3600.")
            .Validate(options => options.QueueLimit >= 0,
                "RateLimiting:Parse:QueueLimit must be zero or greater.")
            .ValidateOnStart();

        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiterOptions.OnRejected = (context, _) =>
            {
                ParseRateLimitOptions options = context.HttpContext.RequestServices
                    .GetRequiredService<IOptions<ParseRateLimitOptions>>().Value;
                context.HttpContext.Response.Headers.RetryAfter =
                    options.WindowSeconds.ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            limiterOptions.AddPolicy(ParsePolicyName, httpContext =>
            {
                ParseRateLimitOptions options = httpContext.RequestServices
                    .GetRequiredService<IOptions<ParseRateLimitOptions>>().Value;

                // Partition per client address so one caller cannot starve others;
                // when the address is unknown (proxies, in-process tests) all
                // requests share a single partition.
                string partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.WindowSeconds),
                    QueueLimit = options.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
        });

        return services;
    }
}
