using Microsoft.AspNetCore.Http.Features;

namespace RoomBook.Api.Problems;

/// <summary>
/// Enforces the request-body limit from `docs/security.md`. The limit is enforced here rather than
/// left to the server: a declared <c>Content-Length</c> over the limit is refused before the body is
/// read at all, and a body that declares no size is read one byte past the limit — if that byte
/// arrives, the request is refused. Lowering the server's own limit is kept as a third line of
/// defence, but nothing depends on the server honouring it, which is what makes the rule testable.
/// </summary>
public static class RequestBodyLimit
{
    public const int MaxBytes = 32 * 1024;

    public static IApplicationBuilder UseRequestBodyLimit(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            IHttpMaxRequestBodySizeFeature? serverLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (serverLimit is { IsReadOnly: false })
            {
                serverLimit.MaxRequestBodySize = MaxBytes;
            }

            if (context.Request.ContentLength > MaxBytes)
            {
                await ErrorResponses.TooLarge(MaxBytes).ExecuteAsync(context);

                return;
            }

            if (context.Request.ContentLength is null && CanCarryABody(context.Request.Method))
            {
                byte[] buffer = new byte[MaxBytes + 1];
                int read;

                try
                {
                    read = await ReadAtMostAsync(context.Request.Body, buffer, context.RequestAborted);
                }
                catch (BadHttpRequestException exception)
                    when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
                {
                    // The server noticed before we finished counting. The answer must still be ours,
                    // or the caller would get a status without the documented `code` member.
                    await ErrorResponses.TooLarge(MaxBytes).ExecuteAsync(context);

                    return;
                }

                if (read > MaxBytes)
                {
                    await ErrorResponses.TooLarge(MaxBytes).ExecuteAsync(context);

                    return;
                }

                // The body has already been consumed, so hand the endpoint what we buffered.
                context.Request.Body = new MemoryStream(buffer, 0, read, writable: false);
                context.Request.ContentLength = read;
            }

            await next(context);
        });

    private static bool CanCarryABody(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method);

    private static async Task<int> ReadAtMostAsync(Stream body, byte[] buffer, CancellationToken cancellationToken)
    {
        int total = 0;

        while (total < buffer.Length)
        {
            int read = await body.ReadAsync(buffer.AsMemory(total), cancellationToken);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
