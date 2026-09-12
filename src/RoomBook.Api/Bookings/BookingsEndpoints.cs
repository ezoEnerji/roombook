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

        routes.MapGet("/bookings/{id:guid}", GetAsync).WithName(GetBookingRouteName);

        return routes;
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
