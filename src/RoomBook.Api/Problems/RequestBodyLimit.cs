using Microsoft.AspNetCore.Http.Features;

namespace RoomBook.Api.Problems;

/// <summary>
/// Enforces the request-body limit from `docs/security.md`. Two mechanisms, because one is not
/// enough: a declared <c>Content-Length</c> over the limit is refused before the body is read at
/// all, and the server's own limit is lowered so a chunked body cannot stream past it either.
/// </summary>
public static class RequestBodyLimit
{
    public const int MaxBytes = 32 * 1024;

    public static IApplicationBuilder UseRequestBodyLimit(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            IHttpMaxRequestBodySizeFeature? limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false })
            {
                limit.MaxRequestBodySize = MaxBytes;
            }

            if (context.Request.ContentLength > MaxBytes)
            {
                await ErrorResponses.TooLarge(MaxBytes).ExecuteAsync(context);

                return;
            }

            await next(context);
        });
}
