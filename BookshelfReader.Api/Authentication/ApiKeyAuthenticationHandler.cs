using System;
using System.Security.Claims;
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
        if (!Options.RequireApiKey)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (Options.ValidKeys.Count == 0)
        {
            Logger.LogError("API key authentication is configured without any valid keys. Rejecting request.");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
        {
            Logger.LogWarning("Request from {RemoteIp} is missing the required API key header {HeaderName}.",
                Context.Connection.RemoteIpAddress, Options.HeaderName);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var providedKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            Logger.LogWarning("Request from {RemoteIp} provided an empty API key header.", Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        foreach (var validKey in Options.ValidKeys)
        {
            if (IsMatch(validKey, providedKey))
            {
                var claims = new[]
                {
                    new Claim(ApiKeyAuthenticationDefaults.ApiKeyClaimType, CreateKeyIdentifier(validKey))
                };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }

        Logger.LogWarning("Request from {RemoteIp} presented an invalid API key.", Context.Connection.RemoteIpAddress);
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

    private static string CreateKeyIdentifier(string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        try
        {
            var hash = SHA256.HashData(keyBytes);
            try
            {
                return Convert.ToHexString(hash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}
