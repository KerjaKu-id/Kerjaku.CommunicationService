using CommunicationService.Tests.Integration.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Tests.Integration.Gateway;

[Collection("GatewayE2E")]
public class GatewayExpirationTests
{
    private readonly GatewayApiClient _api;
    private readonly GatewayTestFixture _fixture;

    public GatewayExpirationTests(GatewayTestFixture fixture)
    {
        _fixture = fixture;
        _api = new GatewayApiClient(fixture.Client);
    }

    [Fact]
    public async Task TemporaryChat_Expires_AndIsMarkedExpired()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(10);

        var room = await _api.CreateRoomAsync(user1, user2, isTemporary: true, expiresAt: expiresAt);

        var expired = await _fixture.WaitForConditionAsync(
            db => db.ChatRooms
                .AsNoTracking()
                .AnyAsync(r => r.Id == room.Id && r.IsExpired),
            _fixture.ExpirationWaitTimeout);

        Assert.True(expired);
    }
}
