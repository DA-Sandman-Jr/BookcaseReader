namespace BookshelfReader.DependencyInjection.Authentication;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string ApiKeyClaimType = "urn:bookshelfreader:apikey";
}
