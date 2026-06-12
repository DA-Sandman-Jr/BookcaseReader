using BookshelfReader.Api.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace BookshelfReader.Api.Extensions;

public static class BookshelfReaderApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services that <see cref="Endpoints.BookshelfReaderEndpointRouteBuilderExtensions.MapBookshelfReaderApi"/>
    /// depends on (image upload validation and request handling). Call this
    /// alongside <c>AddBookshelfReader</c> when mapping the endpoints into your
    /// own host; without it the parse endpoint's handler dependencies are
    /// unresolved and ASP.NET Core treats them as a JSON request body.
    /// </summary>
    public static IServiceCollection AddBookshelfReaderApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IImageUploadValidator, ImageUploadValidator>();
        services.AddSingleton<IImageUploadRequestHandler, ImageUploadRequestHandler>();

        return services;
    }
}
