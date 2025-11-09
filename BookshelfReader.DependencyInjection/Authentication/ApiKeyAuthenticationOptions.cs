using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;

namespace BookshelfReader.DependencyInjection.Authentication;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SectionName = "Authentication:ApiKey";

    public string HeaderName { get; set; } = "X-API-Key";

    public bool RequireApiKey { get; set; } = false;

    public IList<string> ValidKeys { get; set; } = new List<string>();
}
