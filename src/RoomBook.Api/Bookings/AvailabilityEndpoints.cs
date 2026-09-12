using System.Globalization;
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
        if (!TryInt(query, "durationMinutes", out int? durationMinutes) || durationMinutes is null)
        {
            return Invalid("durationMinutes is required and must be a whole number of minutes.");
        }

        if (!TryInstant(query, "from", out DateTimeOffset? from) || from is null)
        {
            return Invalid("from is required and must be a UTC instant ending in 'Z'.");
        }

        if (!TryInstant(query, "to", out DateTimeOffset? to) || to is null)
        {
            return Invalid("to is required and must be a UTC instant ending in 'Z'.");
        }

        Guid? roomId = null;
        if (query.ContainsKey("roomId"))
        {
            if (!Guid.TryParse(query["roomId"], out Guid parsed))
            {
                return Invalid("roomId must be a GUID.");
            }

            roomId = parsed;
        }

        int? attendeeCount = null;
        if (query.ContainsKey("attendeeCount"))
        {
            if (!TryInt(query, "attendeeCount", out int? parsed) || parsed is null or < 1)
            {
                return Invalid("attendeeCount must be a whole number of at least 1.");
            }

            attendeeCount = parsed;
        }

        return Result<SearchAvailabilityQuery>.Success(
            new SearchAvailabilityQuery(durationMinutes.Value, from.Value, to.Value, roomId, attendeeCount));
    }

    private static bool TryInt(IQueryCollection query, string name, out int? value)
    {
        value = null;

        if (!query.ContainsKey(name))
        {
            return false;
        }

        if (!int.TryParse(query[name], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return false;
        }

        value = parsed;

        return true;
    }

    private static bool TryInstant(IQueryCollection query, string name, out DateTimeOffset? value)
    {
        value = null;

        if (!query.ContainsKey(name))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(
                query[name],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset parsed))
        {
            return false;
        }

        if (parsed.Offset != TimeSpan.Zero)
        {
            // BR-5: the wire carries UTC instants, not local times with an offset.
            return false;
        }

        value = parsed;

        return true;
    }

    private static Result<SearchAvailabilityQuery> Invalid(string message) =>
        Result<SearchAvailabilityQuery>.Failure(ErrorCodes.RequestInvalid, message);
}
