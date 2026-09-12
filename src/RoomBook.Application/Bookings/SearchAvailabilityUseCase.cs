using RoomBook.Application.Rooms;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Application.Bookings;

/// <summary>
/// Chooses the rooms worth searching, clamps the window to what the rules allow, asks the domain per
/// room, then merges the answers. It judges no rule itself: the length bounds come from
/// <see cref="Booking"/> and each candidate is validated by <see cref="Booking.Create"/> inside the
/// search.
/// </summary>
public sealed class SearchAvailabilityUseCase
{
    /// <summary>Documented in `docs/conventions.md`: an answer carries at most this many candidates.</summary>
    public const int MaxCandidates = 50;

    /// <summary>Documented in `docs/security.md`: how much time one question may cover.</summary>
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(31);

    private readonly IRoomRepository _rooms;
    private readonly IBookingRepository _bookings;
    private readonly TimeProvider _clock;

    public SearchAvailabilityUseCase(IRoomRepository rooms, IBookingRepository bookings, TimeProvider clock)
    {
        _rooms = rooms;
        _bookings = bookings;
        _clock = clock;
    }

    public async ValueTask<Result<IReadOnlyList<AvailableSlot>>> ExecuteAsync(
        SearchAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        TimeSpan duration = TimeSpan.FromMinutes(query.DurationMinutes);

        if (duration < Booking.MinimumDuration || duration > Booking.MaximumDuration)
        {
            // Refused rather than answered with an empty list: "no booking may be this long" and
            // "nothing is free" are different answers, and the caller acts on them differently.
            return Failure(
                ErrorCodes.DurationOutOfRange,
                $"A booking lasts between {Booking.MinimumDuration.TotalMinutes:0} minutes and {Booking.MaximumDuration.TotalHours:0} hours.");
        }

        Result<TimeSlot> requested = TimeSlot.Create(query.From, query.To);
        if (requested.IsFailure)
        {
            return Failure(requested.Error);
        }

        if (requested.Value.Duration > MaxWindow)
        {
            return Failure(
                ErrorCodes.RequestInvalid,
                $"A search covers at most {MaxWindow.Days} days.");
        }

        Result<IReadOnlyList<Room>> rooms = await ChooseRoomsAsync(query, cancellationToken);
        if (rooms.IsFailure)
        {
            return Failure(rooms.Error);
        }

        DateTimeOffset now = _clock.GetUtcNow();
        DateTimeOffset from = query.From > now ? query.From : now;
        DateTimeOffset until = query.To < now + Booking.BookingHorizon ? query.To : now + Booking.BookingHorizon;

        if (until <= from)
        {
            // The window is entirely behind us or entirely past the horizon. Nothing fits, and that
            // is an answer rather than an error.
            return Success([]);
        }

        TimeSlot searchable = TimeSlot.Create(from, until).Value;
        List<AvailableSlot> candidates = [];

        foreach (Room room in rooms.Value)
        {
            IReadOnlyList<Booking> booked = await _bookings.ListForRoomAsync(room.Id, searchable, cancellationToken);

            candidates.AddRange(AvailabilitySearch.Find(
                room,
                booked,
                searchable,
                duration,
                now,
                query.AttendeeCount ?? 1));
        }

        return Success(candidates
            .OrderBy(candidate => candidate.Slot.Start)
            .ThenBy(candidate => candidate.Room.Name, StringComparer.Ordinal)
            .Take(MaxCandidates)
            .ToList());
    }

    private async ValueTask<Result<IReadOnlyList<Room>>> ChooseRoomsAsync(
        SearchAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Room> rooms;

        if (query.RoomId is Guid roomId)
        {
            Result<Room> room = await _rooms.FindAsync(roomId, cancellationToken);
            if (room.IsFailure)
            {
                return Result<IReadOnlyList<Room>>.Failure(room.Error);
            }

            rooms = [room.Value];
        }
        else
        {
            rooms = await _rooms.GetAllAsync(cancellationToken);
        }

        if (query.AttendeeCount is int attendees)
        {
            // A proposal that would be refused on capacity is not a proposal.
            rooms = rooms.Where(room => room.Capacity >= attendees).ToList();
        }

        return Result<IReadOnlyList<Room>>.Success(rooms);
    }

    private static Result<IReadOnlyList<AvailableSlot>> Success(IReadOnlyList<AvailableSlot> candidates) =>
        Result<IReadOnlyList<AvailableSlot>>.Success(candidates);

    private static Result<IReadOnlyList<AvailableSlot>> Failure(Error error) =>
        Result<IReadOnlyList<AvailableSlot>>.Failure(error);

    private static Result<IReadOnlyList<AvailableSlot>> Failure(string code, string message) =>
        Result<IReadOnlyList<AvailableSlot>>.Failure(code, message);
}
