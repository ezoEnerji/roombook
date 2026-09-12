using RoomBook.Api.Tests.Persistence;
using RoomBook.Domain.Bookings;
using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;
using RoomBook.Infrastructure;

namespace RoomBook.Api.Tests.Infrastructure;

/// <summary>
/// The invariants that only the store can hold, now against SQLite. The assertions are the ones the
/// in-memory adapter had to satisfy, unchanged: what the store is made of is not supposed to matter.
/// <para>
/// A temp file rather than a shared in-memory database, because sixty-four connections to a shared
/// in-memory cache contend on table locks rather than rows — and a concurrency test that fails for a
/// reason unrelated to the guarantee it checks is worse than no test.
/// </para>
/// </summary>
public sealed class SqliteBookingRepositoryTests : IDisposable
{
    private const int Attempts = 64;

    private static readonly DateTimeOffset Now = new(2026, 9, 14, 6, 0, 0, TimeSpan.Zero);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"roombook-{Guid.CreateVersion7():N}.db");
    private readonly RoomBookStore _store;
    private readonly SqliteBookingRepository _bookings;

    public SqliteBookingRepositoryTests()
    {
        _store = new RoomBookStore($"Data Source={_path}");
        _store.EnsureCreated();
        _bookings = new SqliteBookingRepository(_store);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddIfNoOverlapAsync_WhenManyThreadsStoreTheSameWindow_AcceptsExactlyOne()
    {
        Room room = ARoom();
        CancellationToken token = Token;

        IReadOnlyList<Result<Booking>> results = await Task.WhenAll(
            Enumerable.Range(0, Attempts)
                .Select(_ => Task.Run(async () => await _bookings.AddIfNoOverlapAsync(ABooking(room), token), token)));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.All(
            results.Where(result => result.IsFailure),
            result => Assert.Equal(ErrorCodes.BookingOverlap, result.Error.Code));
    }

    [Fact]
    public async Task AddIfNoOverlapAsync_WhenManyThreadsStoreTouchingWindows_AcceptsThemAll()
    {
        // BR-3, and the mirror of the test above: a store that is "safe" by refusing everything
        // would fail here.
        Room room = ARoom();
        CancellationToken token = Token;
        DateTimeOffset first = new(2026, 9, 15, 7, 0, 0, TimeSpan.Zero);

        IReadOnlyList<Result<Booking>> results = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(index => Task.Run(
                    async () => await _bookings.AddIfNoOverlapAsync(
                        ABooking(room, first.AddMinutes(index * 15), first.AddMinutes((index + 1) * 15)),
                        token),
                    token)));

        Assert.All(results, result => Assert.True(result.IsSuccess));
    }

    [Fact]
    public async Task RemoveAsync_WhenManyThreadsCancelTheSameBooking_SucceedsExactlyOnce()
    {
        Room room = ARoom();
        Booking booking = ABooking(room);
        CancellationToken token = Token;

        await _bookings.AddIfNoOverlapAsync(booking, token);

        IReadOnlyList<Result<Booking>> results = await Task.WhenAll(
            Enumerable.Range(0, Attempts)
                .Select(_ => Task.Run(async () => await _bookings.RemoveAsync(booking.Id, Now, token), token)));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.All(
            results.Where(result => result.IsFailure),
            result => Assert.Equal(ErrorCodes.BookingNotFound, result.Error.Code));
    }

    [Fact]
    public async Task RemoveAsync_WhenTheBookingHasAlreadyStarted_ReportsThatRatherThanNotFound()
    {
        // AC-5: the condition and the write are one step, and when the condition refuses, the store
        // says why. Reporting "not found" for a booking that is sitting right there would be cheaper
        // and untrue.
        Room room = ARoom();
        Booking booking = ABooking(room);

        await _bookings.AddIfNoOverlapAsync(booking, Token);

        Result<Booking> result = await _bookings.RemoveAsync(booking.Id, booking.Slot.Start, Token);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.CancelAfterStart, result.Error.Code);

        // And it is still there, because a refused cancellation must not remove anything.
        Assert.True((await _bookings.FindAsync(booking.Id, Token)).IsSuccess);
    }

    [Fact]
    public async Task RemoveAsync_ThenListing_NoLongerReturnsTheBooking()
    {
        Room room = ARoom();
        Booking booking = ABooking(room);

        await _bookings.AddIfNoOverlapAsync(booking, Token);
        await _bookings.RemoveAsync(booking.Id, Now, Token);

        Assert.Empty(await _bookings.ListAsync(booking.Slot, roomId: null, Token));
    }

    [Fact]
    public async Task FindAsync_WhenTheBookingIsAbsent_ReturnsBookingNotFound()
    {
        Result<Booking> result = await _bookings.FindAsync(Guid.CreateVersion7(), Token);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookingNotFound, result.Error.Code);
    }

    [Fact]
    public async Task ListAsync_WithoutARoom_ReturnsEveryRoomsBookings()
    {
        Room first = ARoom();
        Room second = Room.Create(
            Guid.CreateVersion7(),
            "Boğaziçi",
            8,
            "Europe/Istanbul",
            BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;

        await _bookings.AddIfNoOverlapAsync(ABooking(first), Token);
        await _bookings.AddIfNoOverlapAsync(ABooking(second), Token);

        TimeSlot wholeDay = TimeSlot.Create(
            new DateTimeOffset(2026, 9, 15, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero)).Value;

        Assert.Equal(2, (await _bookings.ListAsync(wholeDay, roomId: null, Token)).Count);
        Assert.Single(await _bookings.ListAsync(wholeDay, first.Id, Token));
    }

    [Fact]
    public async Task AddIfNoOverlapAsync_ThenReading_ReturnsWhatWasStored()
    {
        // Rehydration: what comes back out is what went in, including a booking whose start has
        // passed — the rules were judged when it was created, not when it is read.
        Room room = ARoom();
        Booking booking = ABooking(room);

        await _bookings.AddIfNoOverlapAsync(booking, Token);
        Result<Booking> read = await _bookings.FindAsync(booking.Id, Token);

        Assert.Equal(booking.Id, read.Value.Id);
        Assert.Equal(booking.RoomId, read.Value.RoomId);
        Assert.Equal(booking.Title, read.Value.Title);
        Assert.Equal(booking.Organizer, read.Value.Organizer);
        Assert.Equal(booking.Slot.Start, read.Value.Slot.Start);
        Assert.Equal(booking.Slot.End, read.Value.Slot.End);
        Assert.Equal(booking.AttendeeCount, read.Value.AttendeeCount);
    }

    public void Dispose()
    {
        _store.Dispose();
        TempFiles.DeleteBestEffort(_path);
    }

    private static Room ARoom() => Room.Create(
        Guid.CreateVersion7(),
        "Ada",
        8,
        "Europe/Istanbul",
        BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value).Value;

    private static Booking ABooking(Room room, DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        DateTimeOffset from = start ?? new DateTimeOffset(2026, 9, 15, 7, 0, 0, TimeSpan.Zero);

        return Booking.Create(
            Guid.CreateVersion7(),
            room,
            "Sprint review",
            "Abdullah",
            TimeSlot.Create(from, end ?? from.AddHours(1)).Value,
            4,
            Now).Value;
    }
}
