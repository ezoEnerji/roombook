using System.Text.Json;
using RoomBook.Api.Problems;
using RoomBook.Application.Bookings;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Bookings;

public static class BookingsEndpoints
{
    private const string GetBookingRouteName = "GetBooking";

    public static IEndpointRouteBuilder MapBookings(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/bookings", CreateAsync);

        routes.MapGet("/bookings", ListAsync);

        routes.MapGet("/bookings/{id:guid}", GetAsync).WithName(GetBookingRouteName);

        routes.MapDelete("/bookings/{id:guid}", CancelAsync);

        return routes;
    }

    private static async Task<IResult> ListAsync(
        HttpRequest request,
        ListBookingsUseCase listBookings,
        CancellationToken cancellationToken)
    {
        Result<ListBookingsQuery> query = ParseList(request.Query);
        if (query.IsFailure)
        {
            return ErrorResponses.From(query.Error);
        }

        Result<IReadOnlyList<Booking>> bookings = await listBookings.ExecuteAsync(query.Value, cancellationToken);

        return bookings.IsFailure
            ? ErrorResponses.From(bookings.Error)
            : Results.Ok(bookings.Value.Select(BookingResponse.From).ToList());
    }

    private static async Task<IResult> CancelAsync(
        Guid id,
        CancelBookingUseCase cancelBooking,
        CancellationToken cancellationToken)
    {
        Result<Booking> cancelled = await cancelBooking.ExecuteAsync(id, cancellationToken);

        return cancelled.IsFailure
            ? ErrorResponses.From(cancelled.Error)
            : Results.NoContent();
    }

    private static Result<ListBookingsQuery> ParseList(IQueryCollection query)
    {
        if (!QueryValues.TryInstant(query, "from", out DateTimeOffset? from) || from is null)
        {
            return Result<ListBookingsQuery>.Failure(
                ErrorCodes.RequestInvalid,
                "from is required and must be a UTC instant ending in 'Z'.");
        }

        if (!QueryValues.TryInstant(query, "to", out DateTimeOffset? to) || to is null)
        {
            return Result<ListBookingsQuery>.Failure(
                ErrorCodes.RequestInvalid,
                "to is required and must be a UTC instant ending in 'Z'.");
        }

        Guid? roomId = null;
        if (QueryValues.Has(query, "roomId"))
        {
            if (!QueryValues.TryGuid(query, "roomId", out Guid? parsed) || parsed is null)
            {
                return Result<ListBookingsQuery>.Failure(ErrorCodes.RequestInvalid, "roomId must be a GUID.");
            }

            roomId = parsed;
        }

        return Result<ListBookingsQuery>.Success(new ListBookingsQuery(from.Value, to.Value, roomId));
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        CreateBookingUseCase createBooking,
        CancellationToken cancellationToken)
    {
        CreateBookingRequest? request;

        try
        {
            request = await context.Request.ReadFromJsonAsync<CreateBookingRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            // Covers both a body that is not JSON and one carrying a field we do not know: unknown
            // members are rejected by configuration, so a typo cannot be silently ignored.
            return ErrorResponses.Invalid("The request body is not valid JSON, or contains an unknown field.");
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // A chunked body that only reveals its size while being read: the server's limit stops
            // it here rather than in the middleware, and the answer must still be ours.
            return ErrorResponses.TooLarge(RequestBodyLimit.MaxBytes);
        }

        if (request is null)
        {
            return ErrorResponses.Invalid("A request body is required.");
        }

        Result<CreateBookingCommand> command = request.ToCommand();
        if (command.IsFailure)
        {
            return ErrorResponses.From(command.Error);
        }

        Result<Booking> booking = await createBooking.ExecuteAsync(command.Value, cancellationToken);
        if (booking.IsFailure)
        {
            return ErrorResponses.From(booking.Error);
        }

        BookingResponse response = BookingResponse.From(booking.Value);

        // Built from the route name rather than a hand-written path, so renaming the route cannot
        // leave the Location header pointing somewhere that no longer exists.
        return Results.CreatedAtRoute(GetBookingRouteName, new { id = response.Id }, response);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        GetBookingUseCase getBooking,
        CancellationToken cancellationToken)
    {
        Result<Booking> booking = await getBooking.ExecuteAsync(id, cancellationToken);

        return booking.IsFailure
            ? ErrorResponses.From(booking.Error)
            : Results.Ok(BookingResponse.From(booking.Value));
    }
}
