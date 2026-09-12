using RoomBook.Domain.Shared;

namespace RoomBook.Api.Problems;

/// <summary>
/// The one place where an error code becomes an HTTP status. The table is the one written in
/// `docs/conventions.md`, and a code with no entry is a programming mistake rather than a runtime
/// condition — `ErrorResponsesTests` fails if any code is missing, so it cannot reach production.
/// </summary>
public static class ErrorResponses
{
    private const int UnprocessableContent = StatusCodes.Status422UnprocessableEntity;

    public static IReadOnlyDictionary<string, int> StatusByCode { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [ErrorCodes.RequestInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.RequestTooLarge] = StatusCodes.Status413PayloadTooLarge,
        [ErrorCodes.RoomNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.BookingNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.BookingOverlap] = StatusCodes.Status409Conflict,
        [ErrorCodes.OutsideBusinessHours] = UnprocessableContent,
        [ErrorCodes.DurationOutOfRange] = UnprocessableContent,
        [ErrorCodes.AttendeesExceedCapacity] = UnprocessableContent,
        [ErrorCodes.TooFarInFuture] = UnprocessableContent,
        [ErrorCodes.StartInPast] = UnprocessableContent,
    };

    /// <summary>
    /// An RFC 9457 problem document carrying the machine-readable code. The message is the domain's
    /// sentence for a human; nothing else about the failure is disclosed.
    /// </summary>
    public static IResult From(Error error)
    {
        if (!StatusByCode.TryGetValue(error.Code, out int status))
        {
            throw new InvalidOperationException(
                $"Error code '{error.Code}' has no HTTP status. Add it to the table in " +
                "docs/conventions.md and to ErrorResponses.StatusByCode.");
        }

        return Results.Problem(
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    public static IResult Invalid(string message) =>
        From(new Error(ErrorCodes.RequestInvalid, message));

    /// <summary>
    /// A body over the documented limit gets its own code and status rather than joining
    /// <c>request.invalid</c>: a distinct 413 is what lets a test prove the limit is doing the work,
    /// instead of an oversized field triggering a 400 that looks identical.
    /// </summary>
    public static IResult TooLarge(int maxBytes) => From(new Error(
        ErrorCodes.RequestTooLarge,
        $"The request body is limited to {maxBytes / 1024} KB."));
}
