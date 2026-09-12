using Microsoft.Extensions.Time.Testing;
using RoomBook.Api.Tests.Stubs;
using RoomBook.Application.Bookings;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Tests.Bookings;

/// <summary>
/// AC-14 at the use-case level, where the cap lives. The HTTP test proves an answer is capped and
/// ordered; this one proves the stronger property: the answer *is* the earliest fifty of everything
/// available, so any truncation before the global ordering changes the result.
/// <para>
/// Honest limit: the expected set is computed with the same domain search the code uses, so this
/// catches ordering and truncation mistakes rather than mistakes inside the search itself. And one
/// hypothetical — slicing each room to fifty before merging — turns out not to be observable with
/// any reasonable fixture, because the candidates such a slice discards are the latest ones, which
/// the global cap would discard anyway. That one is guarded by there being a single `Take` in the
/// code, not by this test, and saying so is better than pretending otherwise.
/// </para>
/// </summary>
public sealed class SearchAvailabilityCapTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 6, 0, 0, TimeSpan.Zero);

    private static readonly Guid FragmentedRoomId = Guid.Parse("0192a1b2-c3d4-7a01-8b01-000000000001");

    private static readonly Guid FreeRoomId = Guid.Parse("0192a1b2-c3d4-7a02-8b02-000000000002");

    [Fact]
    public async Task ExecuteAsync_WhenMoreThanFiftyCandidatesExist_ReturnsExactlyTheEarliestFifty()
    {
        Room fragmented = RoomNamed("Ada", FragmentedRoomId);
        Room free = RoomNamed("Boğaziçi", FreeRoomId);
        IReadOnlyList<Booking> bookings = FragmentThreeDays(fragmented);

        SearchAvailabilityQuery query = new(
            DurationMinutes: 15,
            From: new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero),
            To: new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero),
            RoomId: null,
            AttendeeCount: null);

        SearchAvailabilityUseCase useCase = new(
            new StubRoomRepository([fragmented, free]),
            new StubBookingRepository(bookings),
            new FakeTimeProvider(Now));

        Result<IReadOnlyList<AvailableSlot>> result =
            await useCase.ExecuteAsync(query, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SearchAvailabilityUseCase.MaxCandidates, result.Value.Count);

        IReadOnlyList<(Guid Room, DateTimeOffset Start)> expected =
            EarliestFifty([fragmented, free], bookings, query);

        Assert.Equal(expected, result.Value.Select(slot => (slot.Room.Id, slot.Slot.Start)));

        // Both rooms are represented: an answer drawn from one room only would satisfy "fifty,
        // ordered" and still be the wrong answer.
        Assert.Contains(result.Value, slot => slot.Room.Id == FragmentedRoomId);
        Assert.Contains(result.Value, slot => slot.Room.Id == FreeRoomId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFewerThanFiftyCandidatesExist_ReturnsThemAll()
    {
        Room free = RoomNamed("Boğaziçi", FreeRoomId);

        SearchAvailabilityQuery query = new(
            DurationMinutes: 60,
            From: new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero),
            To: new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero),
            RoomId: null,
            AttendeeCount: null);

        SearchAvailabilityUseCase useCase = new(
            new StubRoomRepository([free]),
            new StubBookingRepository([]),
            new FakeTimeProvider(Now));

        Result<IReadOnlyList<AvailableSlot>> result =
            await useCase.ExecuteAsync(query, TestContext.Current.CancellationToken);

        // Three free days, one candidate per day: the cap must not invent a limit of its own.
        Assert.Equal(3, result.Value.Count);
    }

    /// <summary>
    /// The answer the use case ought to give, assembled independently of it: every candidate from
    /// every room, ordered by start and then by room name, cut at the cap.
    /// </summary>
    private static IReadOnlyList<(Guid Room, DateTimeOffset Start)> EarliestFifty(
        IReadOnlyList<Room> rooms,
        IReadOnlyList<Booking> bookings,
        SearchAvailabilityQuery query)
    {
        TimeSlot window = TimeSlot.Create(query.From > Now ? query.From : Now, query.To).Value;

        return rooms
            .SelectMany(room => AvailabilitySearch.Find(
                room,
                bookings.Where(booking => booking.RoomId == room.Id).ToList(),
                window,
                TimeSpan.FromMinutes(query.DurationMinutes),
                Now))
            .OrderBy(slot => slot.Slot.Start)
            .ThenBy(slot => slot.Room.Name, StringComparer.Ordinal)
            .Take(SearchAvailabilityUseCase.MaxCandidates)
            .Select(slot => (slot.Room.Id, slot.Slot.Start))
            .ToList();
    }

    /// <summary>
    /// Books every other quarter hour for three days, leaving eighteen fifteen-minute gaps a day —
    /// fifty-four candidates from one room, which is what puts the cap under real pressure.
    /// </summary>
    private static IReadOnlyList<Booking> FragmentThreeDays(Room room)
    {
        List<Booking> bookings = [];

        foreach (int day in new[] { 15, 16, 17 })
        {
            DateTimeOffset openedAt = new(2026, 9, day, 6, 0, 0, TimeSpan.Zero);

            for (int slot = 0; slot < 18; slot++)
            {
                DateTimeOffset start = openedAt.AddMinutes(slot * 30);

                bookings.Add(Booking.Create(
                    Guid.CreateVersion7(),
                    room,
                    "Existing",
                    "Abdullah",
                    TimeSlot.Create(start, start.AddMinutes(15)).Value,
                    1,
                    Now).Value);
            }
        }

        return bookings;
    }

    private static Room RoomNamed(string name, Guid id) => Room.Create(
        id,
        name,
        8,
        "Europe/Istanbul",
        BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;

}
