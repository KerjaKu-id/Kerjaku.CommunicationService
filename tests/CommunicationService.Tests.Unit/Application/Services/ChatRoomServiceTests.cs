using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Application.Exceptions;
using CommunicationService.Application.Options;
using CommunicationService.Application.Services;
using CommunicationService.Domain.Entities;
using CommunicationService.Tests.Unit.TestUtilities;
using Moq;

namespace CommunicationService.Tests.Unit.Application.Services;

public class ChatRoomServiceTests
{
    [Fact]
    public async Task CreateRoomAsync_Throws_WhenNotEnoughParticipants()
    {
        var repo = new Mock<IChatRoomRepository>();
        var messageRepo = new Mock<IMessageRepository>();
        var statusRepo = new Mock<IMessageStatusRepository>();
        var userShadowRepo = new Mock<IUserShadowRepository>();
        var service = new ChatRoomService(
            repo.Object,
            messageRepo.Object,
            statusRepo.Object,
            userShadowRepo.Object,
            new TemporaryChatOptions { DefaultTtlHours = 4 },
            new FakeDateTimeProvider(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)));

        var request = new CreateChatRoomRequest
        {
            ParticipantIds = new[] { Guid.NewGuid() }
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateRoomAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateRoomAsync_Throws_WhenParticipantsNotUnique()
    {
        var repo = new Mock<IChatRoomRepository>();
        var messageRepo = new Mock<IMessageRepository>();
        var statusRepo = new Mock<IMessageStatusRepository>();
        var userShadowRepo = new Mock<IUserShadowRepository>();
        var service = new ChatRoomService(
            repo.Object,
            messageRepo.Object,
            statusRepo.Object,
            userShadowRepo.Object,
            new TemporaryChatOptions { DefaultTtlHours = 4 },
            new FakeDateTimeProvider(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)));

        var duplicateUser = Guid.NewGuid();
        var request = new CreateChatRoomRequest
        {
            ParticipantIds = new[] { duplicateUser, duplicateUser }
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateRoomAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateRoomAsync_UsesDefaultTtl_ForTemporaryRooms()
    {
        var repo = new Mock<IChatRoomRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<ChatRoom>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var messageRepo = new Mock<IMessageRepository>();
        var statusRepo = new Mock<IMessageStatusRepository>();
        var userShadowRepo = new Mock<IUserShadowRepository>();

        var now = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
        var service = new ChatRoomService(
            repo.Object,
            messageRepo.Object,
            statusRepo.Object,
            userShadowRepo.Object,
            new TemporaryChatOptions { DefaultTtlHours = 6 },
            new FakeDateTimeProvider(now));

        var request = new CreateChatRoomRequest
        {
            ParticipantIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
            IsTemporary = true
        };

        var room = await service.CreateRoomAsync(request, CancellationToken.None);

        Assert.True(room.IsTemporary);
        Assert.Equal(now.AddHours(6), room.ExpiresAt);
        repo.Verify(r => r.AddAsync(It.IsAny<ChatRoom>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRoomAsync_Throws_WhenExpirationIsInPast()
    {
        var repo = new Mock<IChatRoomRepository>();
        var messageRepo = new Mock<IMessageRepository>();
        var statusRepo = new Mock<IMessageStatusRepository>();
        var userShadowRepo = new Mock<IUserShadowRepository>();
        var now = new DateTimeOffset(2026, 4, 30, 10, 0, 0, TimeSpan.Zero);
        var service = new ChatRoomService(
            repo.Object,
            messageRepo.Object,
            statusRepo.Object,
            userShadowRepo.Object,
            new TemporaryChatOptions { DefaultTtlHours = 4 },
            new FakeDateTimeProvider(now));

        var request = new CreateChatRoomRequest
        {
            ParticipantIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
            IsTemporary = true,
            ExpiresAt = now.AddMinutes(-5)
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateRoomAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetRoomAsync_Throws_WhenRoomNotFound()
    {
        var repo = new Mock<IChatRoomRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatRoom?)null);
        var messageRepo = new Mock<IMessageRepository>();
        var statusRepo = new Mock<IMessageStatusRepository>();
        var userShadowRepo = new Mock<IUserShadowRepository>();

        var service = new ChatRoomService(
            repo.Object,
            messageRepo.Object,
            statusRepo.Object,
            userShadowRepo.Object,
            new TemporaryChatOptions { DefaultTtlHours = 4 },
            new FakeDateTimeProvider(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetRoomAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetRoomAsync_Throws_WhenRoomExpired()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var room = new ChatRoom(true, now.AddMinutes(-1), now.AddHours(-1));
        var repo = new Mock<IChatRoomRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);
        var messageRepo = new Mock<IMessageRepository>();
        var statusRepo = new Mock<IMessageStatusRepository>();
        var userShadowRepo = new Mock<IUserShadowRepository>();

        var service = new ChatRoomService(
            repo.Object,
            messageRepo.Object,
            statusRepo.Object,
            userShadowRepo.Object,
            new TemporaryChatOptions { DefaultTtlHours = 4 },
            new FakeDateTimeProvider(now));

        await Assert.ThrowsAsync<ResourceExpiredException>(() => service.GetRoomAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetRoomsForUserAsync_ReturnsRooms_WhenRoomHasNoShadowUserData()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var viewerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var room = new ChatRoom(false, null, now);
        room.AddParticipant(viewerId, now);
        room.AddParticipant(otherId, now);

        var repo = new Mock<IChatRoomRepository>();
        repo.Setup(r => r.GetByParticipantAsync(viewerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { room });

        var messageRepo = new Mock<IMessageRepository>();
        messageRepo.Setup(r => r.GetLatestByRoomIdAsync(room.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var statusRepo = new Mock<IMessageStatusRepository>();
        statusRepo.Setup(r => r.CountUnreadByRoomAsync(room.Id, viewerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var userShadowRepo = new Mock<IUserShadowRepository>();
        userShadowRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, UserShadow>());

        var service = new ChatRoomService(
            repo.Object,
            messageRepo.Object,
            statusRepo.Object,
            userShadowRepo.Object,
            new TemporaryChatOptions { DefaultTtlHours = 4 },
            new FakeDateTimeProvider(now));

        var rooms = await service.GetRoomsForUserAsync(viewerId, CancellationToken.None);

        Assert.Single(rooms);
        Assert.Equal(room.Id, rooms.Single().Id);
        Assert.Null(rooms.Single().OtherPartyId);
        Assert.Null(rooms.Single().OtherPartyName);
        Assert.Equal(0, rooms.Single().UnreadCount);
    }
}
