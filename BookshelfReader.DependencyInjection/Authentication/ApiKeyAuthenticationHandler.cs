using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookshelfReader.DependencyInjection.Authentication;

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
            if (ApiKeyValidator.IsMatch(validKey, providedKey))
            {
                var claims = new[]
                {
                    new Claim(ApiKeyAuthenticationDefaults.ApiKeyClaimType, ApiKeyValidator.CreateKeyIdentifier(validKey))
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

}
