using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomBook.Api.Infrastructure;
using RoomBook.Application.Rooms;
using RoomBook.Domain.Rooms;

namespace RoomBook.Api.Tests;

/// <summary>
/// Boots the real composition root. Passing rooms replaces the seeded set, which is how the
/// empty-installation case is reachable at all — and proof that seeding is a composition-root
/// concern rather than something baked into the storage adapter.
/// </summary>
internal sealed class RoomBookApplication : WebApplicationFactory<Program>
{
    private readonly IReadOnlyList<Room>? _rooms;

    public RoomBookApplication(IReadOnlyList<Room>? rooms = null)
    {
        _rooms = rooms;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_rooms is null)
        {
            return;
        }

        IReadOnlyList<Room> rooms = _rooms;

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRoomRepository>();
            services.AddSingleton<IRoomRepository>(_ => new InMemoryRoomRepository(rooms));
        });
    }
}
