using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Tests.Rooms;

public sealed class RoomTests
{
    private static readonly Guid AnyId = Guid.Parse("0192a1b2-c3d4-7a01-8b01-000000000001");

    [Fact]
    public void Create_WithValidValues_ReturnsRoom()
    {
        Result<Room> result = Create(capacity: 8, timeZoneId: "Europe/Istanbul");

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", result.Value.Name);
        Assert.Equal(8, result.Value.Capacity);
        Assert.Equal("Europe/Istanbul", result.Value.TimeZoneId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenCapacityIsBelowOne_ReturnsError(int capacity)
    {
        Result<Room> result = Create(capacity: capacity, timeZoneId: "Europe/Istanbul");

        Assert.True(result.IsFailure);
        Assert.Equal(Room.CapacityBelowMinimumCode, result.Error.Code);
    }

    [Fact]
    public void Create_WhenCapacityIsExactlyOne_ReturnsRoom()
    {
        Result<Room> result = Create(capacity: 1, timeZoneId: "Europe/Istanbul");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenTimeZoneCannotBeResolved_ReturnsError(string timeZoneId)
    {
        Result<Room> result = Create(capacity: 8, timeZoneId: timeZoneId);

        Assert.True(result.IsFailure);
        Assert.Equal(Room.TimeZoneUnknownCode, result.Error.Code);
    }

    [Fact]
    public void Create_WhenTimeZoneIsAnIanaIdentifier_ReturnsRoom()
    {
        // The identifier must resolve on Windows and Linux alike — the CI runner is Linux and the
        // authoring machine is Windows, so an IANA id that only works on one of them is a defect.
        Result<Room> result = Create(capacity: 8, timeZoneId: "Europe/Berlin");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Value_OnAFailedResult_Throws()
    {
        Result<Room> result = Create(capacity: 0, timeZoneId: "Europe/Istanbul");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    private static Result<Room> Create(int capacity, string timeZoneId)
    {
        BusinessHours hours = BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value;

        return Room.Create(AnyId, "Ada", capacity, timeZoneId, hours);
    }
}
