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
/// Boots the real composition root. Passing rooms replaces the seeded set — which is how the
/// empty-installation case is reachable at all — and passing a clock makes the time-dependent rules
/// deterministic instead of depending on the day the suite happens to run.
/// </summary>
internal sealed class RoomBookApplication : WebApplicationFactory<Program>
{
    private readonly IReadOnlyList<Room>? _rooms;
    private readonly TimeProvider? _clock;

    public RoomBookApplication(IReadOnlyList<Room>? rooms = null, TimeProvider? clock = null)
    {
        _rooms = rooms;
        _clock = clock;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        IReadOnlyList<Room>? rooms = _rooms;
        TimeProvider? clock = _clock;

        builder.ConfigureTestServices(services =>
        {
            if (rooms is not null)
            {
                services.RemoveAll<IRoomRepository>();
                services.AddSingleton<IRoomRepository>(_ => new InMemoryRoomRepository(rooms));
            }

            if (clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            }
        });
    }
}
