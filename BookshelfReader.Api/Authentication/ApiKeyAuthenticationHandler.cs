using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookshelfReader.Api.Authentication;

internal sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Options.ValidKeys.Count == 0)
        {
            Logger.LogWarning("API key authentication is configured without any valid keys.");
            return Task.FromResult(AuthenticateResult.Fail("No API keys configured."));
        }

        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing API key header."));
        }

        var providedKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key header is empty."));
        }

        foreach (var validKey in Options.ValidKeys)
        {
            if (IsMatch(validKey, providedKey))
            {
                var claims = new[] { new System.Security.Claims.Claim("ApiKey", Options.HeaderName) };
                var identity = new System.Security.Claims.ClaimsIdentity(claims, Scheme.Name);
                var principal = new System.Security.Claims.ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
    }

    private static bool IsMatch(string? expectedKey, string providedKey)
    {
        if (string.IsNullOrWhiteSpace(expectedKey) || string.IsNullOrEmpty(providedKey))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        try
        {
            var expectedHash = SHA256.HashData(expectedBytes);
            var providedHash = SHA256.HashData(providedBytes);

            try
            {
                return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expectedHash);
                CryptographicOperations.ZeroMemory(providedHash);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedBytes);
            CryptographicOperations.ZeroMemory(providedBytes);
        }
    }
}
