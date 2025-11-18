using System;
using BookshelfReader.DependencyInjection.Authentication;
using Microsoft.AspNetCore.Authentication;

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
