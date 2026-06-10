using Microsoft.AspNetCore.Authentication;

namespace BookshelfReader.Extensions.Authentication;

public static class BookshelfReaderAuthenticationBuilderExtensions
{
    public static AuthenticationBuilder AddBookshelfReaderApiKey(this AuthenticationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationDefaults.AuthenticationScheme,
            _ => { });
    }
}
