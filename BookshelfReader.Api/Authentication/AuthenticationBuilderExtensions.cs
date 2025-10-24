using System;
using Microsoft.AspNetCore.Authentication;

namespace BookshelfReader.Api.Authentication;

public static class AuthenticationBuilderExtensions
{
    public static AuthenticationBuilder AddBookshelfReaderApiKey(this AuthenticationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationDefaults.AuthenticationScheme,
            _ => { });
    }
}
