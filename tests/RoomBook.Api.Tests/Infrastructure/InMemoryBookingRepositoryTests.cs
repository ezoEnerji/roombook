using RoomBook.Api.Infrastructure;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Tests.Infrastructure;

/// <summary>
/// BR-2 is an invariant, so it is tested where it is enforced. The HTTP race test can only attempt
/// concurrency — if the test host happened to serialise the two requests, a check-then-write
/// implementation would still produce one success and one conflict and the test would pass. These
/// hit the adapter directly from many threads, where a missing lock cannot hide.
/// </summary>
public sealed class InMemoryBookingRepositoryTests
{
    private const int Attempts = 64;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddIfNoOverlapAsync_WhenManyThreadsStoreTheSameWindow_AcceptsExactlyOne()
    {
        InMemoryBookingRepository repository = new();
        Room room = Room();
        CancellationToken token = Token;

        IReadOnlyList<Result<Booking>> results = await Task.WhenAll(
            Enumerable.Range(0, Attempts)
                .Select(_ => Task.Run(async () => await repository.AddIfNoOverlapAsync(Booking(room), token), token)));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.All(
            results.Where(result => result.IsFailure),
            result => Assert.Equal(ErrorCodes.BookingOverlap, result.Error.Code));
    }

    [Fact]
    public async Task AddIfNoOverlapAsync_WhenManyThreadsStoreTouchingWindows_AcceptsThemAll()
    {
        // The mirror case: back-to-back bookings must all survive concurrency, or the lock would be
        // "safe" by simply rejecting everything (BR-3).
        InMemoryBookingRepository repository = new();
        Room room = Room();
        CancellationToken token = Token;
        DateTimeOffset first = new(2026, 9, 14, 7, 0, 0, TimeSpan.Zero);

        IReadOnlyList<Result<Booking>> results = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(index => Task.Run(
                    async () => await repository.AddIfNoOverlapAsync(
                        Booking(room, first.AddMinutes(index * 15), first.AddMinutes((index + 1) * 15)),
                        token),
                    token)));

        Assert.All(results, result => Assert.True(result.IsSuccess));
    }

    [Fact]
    public async Task FindAsync_WhenTheBookingIsAbsent_ReturnsBookingNotFound()
    {
        InMemoryBookingRepository repository = new();

        Result<Booking> result = await repository.FindAsync(Guid.CreateVersion7(), Token);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookingNotFound, result.Error.Code);
    }

    private static Room Room() => Domain.Rooms.Room.Create(
        Guid.CreateVersion7(),
        "Ada",
        8,
        "Europe/Istanbul",
        BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;

    private static Booking Booking(Room room, DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        DateTimeOffset from = start ?? new DateTimeOffset(2026, 9, 14, 7, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = end ?? from.AddHours(1);

        return Domain.Bookings.Booking.Create(
            Guid.CreateVersion7(),
            room,
            "Sprint review",
            "Abdullah",
            TimeSlot.Create(from, to).Value,
            4,
            from.AddDays(-1)).Value;
    }
}
