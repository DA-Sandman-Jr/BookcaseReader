using System;
using Microsoft.AspNetCore.Authentication;
using BookshelfReader.DependencyInjection.Authentication;

namespace Microsoft.AspNetCore.Authentication;

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
