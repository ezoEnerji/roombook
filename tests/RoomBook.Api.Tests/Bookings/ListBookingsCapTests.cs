using RoomBook.Api.Tests.Stubs;
using RoomBook.Application.Bookings;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Tests.Bookings;

/// <summary>
/// AC-12 where the cap lives. Like the availability cap test, this asserts the stronger property —
/// the answer *is* the earliest two hundred — rather than only that two hundred came back sorted.
/// </summary>
public sealed class ListBookingsCapTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_WhenMoreThanTwoHundredBookingsMatch_ReturnsTheEarliestTwoHundred()
    {
        Room room = RoomNamed("Ada");
        IReadOnlyList<Booking> bookings = FillTenDays(room);

        ListBookingsUseCase useCase = new(
            new StubRoomRepository([room]),
            new StubBookingRepository(bookings));

        Result<IReadOnlyList<Booking>> result = await useCase.ExecuteAsync(
            new ListBookingsQuery(
                From: new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero),
                To: new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero),
                RoomId: null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ListBookingsUseCase.MaxBookings, result.Value.Count);

        IReadOnlyList<DateTimeOffset> expected = bookings
            .OrderBy(booking => booking.Slot.Start)
            .Take(ListBookingsUseCase.MaxBookings)
            .Select(booking => booking.Slot.Start)
            .ToList();

        Assert.Equal(expected, result.Value.Select(booking => booking.Slot.Start));
        Assert.True(bookings.Count > ListBookingsUseCase.MaxBookings, "The fixture must exceed the cap.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenFewerBookingsMatch_ReturnsThemAll()
    {
        Room room = RoomNamed("Ada");
        IReadOnlyList<Booking> bookings = [BookingAt(room, new DateTimeOffset(2026, 9, 15, 7, 0, 0, TimeSpan.Zero))];

        ListBookingsUseCase useCase = new(
            new StubRoomRepository([room]),
            new StubBookingRepository(bookings));

        Result<IReadOnlyList<Booking>> result = await useCase.ExecuteAsync(
            new ListBookingsQuery(
                From: new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero),
                To: new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero),
                RoomId: null),
            TestContext.Current.CancellationToken);

        Assert.Single(result.Value);
    }

    /// <summary>
    /// Thirty-six quarter-hour bookings a day for ten days: three hundred and sixty, comfortably
    /// past the cap, which is what puts it under real pressure.
    /// </summary>
    private static IReadOnlyList<Booking> FillTenDays(Room room)
    {
        List<Booking> bookings = [];

        for (int day = 15; day < 25; day++)
        {
            DateTimeOffset openedAt = new(2026, 9, day, 6, 0, 0, TimeSpan.Zero);

            for (int slot = 0; slot < 36; slot++)
            {
                bookings.Add(BookingAt(room, openedAt.AddMinutes(slot * 15)));
            }
        }

        return bookings;
    }

    private static Booking BookingAt(Room room, DateTimeOffset start) => Booking.Create(
        Guid.CreateVersion7(),
        room,
        "Existing",
        "Abdullah",
        TimeSlot.Create(start, start.AddMinutes(15)).Value,
        1,
        Now).Value;

    private static Room RoomNamed(string name) => Room.Create(
        Guid.Parse("0192a1b2-c3d4-7a01-8b01-000000000001"),
        name,
        8,
        "Europe/Istanbul",
        BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;
}
