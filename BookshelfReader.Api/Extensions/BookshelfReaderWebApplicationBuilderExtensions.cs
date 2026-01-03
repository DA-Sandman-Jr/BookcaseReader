using System;
using System.Threading.Tasks;
using BookshelfReader.Api.Endpoints;
using BookshelfReader.Api.Validation;
using BookshelfReader.DependencyInjection.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BookshelfReader.Api.Extensions;

public static class BookshelfReaderWebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddBookshelfReaderHost(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddBookshelfReader(builder.Configuration);

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = ApiKeyAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = ApiKeyAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = ApiKeyAuthenticationDefaults.AuthenticationScheme;
            })
            .AddBookshelfReaderApiKey();
        builder.Services.AddAuthorization();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<IImageUploadValidator, ImageUploadValidator>();
        builder.Services.AddSingleton<IImageUploadRequestHandler, ImageUploadRequestHandler>();

        return builder;
    }

    public static WebApplication UseBookshelfReaderHost(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseAuthentication();
        app.UseAuthorization();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.Use(SetSecurityHeadersAsync);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapBookshelfReaderApi();

        return app;
    }

    private static Task SetSecurityHeadersAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cache-Control"] = "no-store";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
