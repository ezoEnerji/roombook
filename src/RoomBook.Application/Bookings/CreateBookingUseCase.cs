using RoomBook.Application.Rooms;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Application.Bookings;

/// <summary>
/// Creates a booking. This layer owns the clock and hands the instant inward as a value, so the
/// domain stays free of any time dependency (FD-3). It evaluates nothing itself: the order in which
/// rules are judged lives in <see cref="Booking.Create"/>, in one place, so the API and the domain
/// cannot drift apart.
/// </summary>
public sealed class CreateBookingUseCase
{
    private readonly IRoomRepository _rooms;
    private readonly IBookingRepository _bookings;
    private readonly TimeProvider _clock;

    public CreateBookingUseCase(IRoomRepository rooms, IBookingRepository bookings, TimeProvider clock)
    {
        _rooms = rooms;
        _bookings = bookings;
        _clock = clock;
    }

    public async ValueTask<Result<Booking>> ExecuteAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken)
    {
        Result<TimeSlot> slot = TimeSlot.Create(command.Start, command.End);
        if (slot.IsFailure)
        {
            return Result<Booking>.Failure(slot.Error);
        }

        Result<Room> room = await _rooms.FindAsync(command.RoomId, cancellationToken);
        if (room.IsFailure)
        {
            return Result<Booking>.Failure(room.Error);
        }

        Result<Booking> booking = Booking.Create(
            Guid.CreateVersion7(),
            room.Value,
            command.Title,
            command.Organizer,
            slot.Value,
            command.AttendeeCount,
            _clock.GetUtcNow());

        if (booking.IsFailure)
        {
            return booking;
        }

        return await _bookings.AddIfNoOverlapAsync(booking.Value, cancellationToken);
    }
}
