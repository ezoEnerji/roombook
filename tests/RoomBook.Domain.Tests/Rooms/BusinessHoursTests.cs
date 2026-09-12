using RoomBook.Domain.Rooms;
using RoomBook.Domain.Shared;

namespace RoomBook.Domain.Tests.Rooms;

public sealed class BusinessHoursTests
{
    [Fact]
    public void Create_WhenOpenIsBeforeClose_ReturnsHours()
    {
        Result<BusinessHours> result = BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(new TimeOnly(9, 0), result.Value.Open);
        Assert.Equal(new TimeOnly(18, 0), result.Value.Close);
    }

    [Fact]
    public void Create_WhenOpenEqualsClose_ReturnsError()
    {
        Result<BusinessHours> result = BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(9, 0));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessHours.InvalidWindowCode, result.Error.Code);
    }

    [Fact]
    public void Create_WhenOpenIsAfterClose_ReturnsError()
    {
        Result<BusinessHours> result = BusinessHours.Create(new TimeOnly(18, 0), new TimeOnly(9, 0));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessHours.InvalidWindowCode, result.Error.Code);
    }

    [Fact]
    public void Create_WhenWindowIsOneMinuteWide_ReturnsHours()
    {
        Result<BusinessHours> result = BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(9, 1));

        Assert.True(result.IsSuccess);
    }
}
