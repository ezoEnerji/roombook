using RoomBook.Api.Problems;
using RoomBook.Application.Bookings;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Bookings;

public static class AvailabilityEndpoints
{
    public static IEndpointRouteBuilder MapAvailability(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/availability", SearchAsync);

        return routes;
    }

    private static async Task<IResult> SearchAsync(
        HttpRequest request,
        SearchAvailabilityUseCase searchAvailability,
        CancellationToken cancellationToken)
    {
        // The query string is parsed here rather than by model binding, so a malformed value gets the
        // documented error contract instead of the framework's own 400 body.
        Result<SearchAvailabilityQuery> query = Parse(request.Query);
        if (query.IsFailure)
        {
            return ErrorResponses.From(query.Error);
        }

        Result<IReadOnlyList<AvailableSlot>> candidates =
            await searchAvailability.ExecuteAsync(query.Value, cancellationToken);

        return candidates.IsFailure
            ? ErrorResponses.From(candidates.Error)
            : Results.Ok(candidates.Value.Select(AvailableSlotResponse.From).ToList());
    }

    private static Result<SearchAvailabilityQuery> Parse(IQueryCollection query)
    {
        if (!QueryValues.TryInt(query, "durationMinutes", out int? durationMinutes) || durationMinutes is null)
        {
            return Invalid("durationMinutes is required and must be a whole number of minutes.");
        }

        if (!QueryValues.TryInstant(query, "from", out DateTimeOffset? from) || from is null)
        {
            return Invalid("from is required and must be a UTC instant ending in 'Z'.");
        }

        if (!QueryValues.TryInstant(query, "to", out DateTimeOffset? to) || to is null)
        {
            return Invalid("to is required and must be a UTC instant ending in 'Z'.");
        }

        Guid? roomId = null;
        if (QueryValues.Has(query, "roomId"))
        {
            if (!QueryValues.TryGuid(query, "roomId", out Guid? parsed) || parsed is null)
            {
                return Invalid("roomId must be a GUID.");
            }

            roomId = parsed;
        }

        int? attendeeCount = null;
        if (QueryValues.Has(query, "attendeeCount"))
        {
            if (!QueryValues.TryInt(query, "attendeeCount", out int? parsed) || parsed is null or < 1)
            {
                return Invalid("attendeeCount must be a whole number of at least 1.");
            }

            attendeeCount = parsed;
        }

        return Result<SearchAvailabilityQuery>.Success(
            new SearchAvailabilityQuery(durationMinutes.Value, from.Value, to.Value, roomId, attendeeCount));
    }

    private static Result<SearchAvailabilityQuery> Invalid(string message) =>
        Result<SearchAvailabilityQuery>.Failure(ErrorCodes.RequestInvalid, message);
}
