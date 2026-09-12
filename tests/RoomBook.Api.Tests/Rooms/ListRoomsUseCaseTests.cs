using RoomBook.Application.Rooms;
using RoomBook.Domain.Rooms;

namespace RoomBook.Api.Tests.Rooms;

public sealed class ListRoomsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenNamesWouldSortDifferentlyByCulture_SortsOrdinally()
    {
        // Ordinal comparison orders by code point: "Ada" (A=65), "Boğaziçi" (B=66), "Zeugma" (Z=90),
        // "ada" (a=97). A culture-aware sort would place "ada" next to "Ada" on one machine and
        // elsewhere on another; the contract is that every machine agrees.
        ListRoomsUseCase useCase = new(new StubRoomRepository([
            NamedRoom("ada"),
            NamedRoom("Zeugma"),
            NamedRoom("Boğaziçi"),
            NamedRoom("Ada"),
        ]));

        IReadOnlyList<Room> rooms = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Ada", "Boğaziçi", "Zeugma", "ada"], rooms.Select(room => room.Name));
    }

    [Fact]
    public async Task ExecuteAsync_WhenThereAreNoRooms_ReturnsEmpty()
    {
        ListRoomsUseCase useCase = new(new StubRoomRepository([]));

        IReadOnlyList<Room> rooms = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Empty(rooms);
    }

    private static Room NamedRoom(string name)
    {
        BusinessHours hours = BusinessHours.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)).Value;

        return Room.Create(Guid.CreateVersion7(), name, 4, "Europe/Istanbul", hours).Value;
    }

    private sealed class StubRoomRepository : IRoomRepository
    {
        private readonly IReadOnlyList<Room> _rooms;

        public StubRoomRepository(IReadOnlyList<Room> rooms)
        {
            _rooms = rooms;
        }

        public ValueTask<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(_rooms);
    }
}
