using CommunicationService.Application.Abstractions.Events;
using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Application.Events;
using CommunicationService.Application.Exceptions;
using CommunicationService.Application.Services;
using CommunicationService.Domain.Entities;
using CommunicationService.Domain.Enums;
using CommunicationService.Tests.Unit.TestUtilities;
using Moq;

namespace CommunicationService.Tests.Unit.Application.Services;

public class MessageServiceTests
{
    [Fact]
    public async Task SendMessageAsync_Throws_WhenContentMissing()
    {
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            new FakeDateTimeProvider(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)));
        var request = new SendMessageRequest
        {
            RoomId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            Type = MessageType.Text,
            Content = "  "
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.SendMessageAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendMessageAsync_Throws_WhenImageUrlInvalid()
    {
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            new FakeDateTimeProvider(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)));
        var request = new SendMessageRequest
        {
            RoomId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            Type = MessageType.Image,
            Content = "not-a-url"
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.SendMessageAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendMessageAsync_Throws_WhenRoomNotFound()
    {
        var chatRoomRepo = new Mock<IChatRoomRepository>();
        chatRoomRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatRoom?)null);

        var service = CreateService(
            chatRoomRepo,
            out _,
            out _,
            out _,
            new FakeDateTimeProvider(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)));
        var request = new SendMessageRequest
        {
            RoomId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            Type = MessageType.Text,
            Content = "hello"
        };

        await Assert.ThrowsAsync<NotFoundException>(() => service.SendMessageAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendMessageAsync_Throws_WhenRoomExpired()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var room = new ChatRoom(true, now.AddMinutes(-5), now.AddHours(-1));
        var chatRoomRepo = new Mock<IChatRoomRepository>();
        chatRoomRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        var service = CreateService(chatRoomRepo, out _, out _, out _, new FakeDateTimeProvider(now));
        var request = new SendMessageRequest
        {
            RoomId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            Type = MessageType.Text,
            Content = "hello"
        };

        await Assert.ThrowsAsync<ResourceExpiredException>(() => service.SendMessageAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendMessageAsync_Throws_WhenSenderNotParticipant()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var room = new ChatRoom(false, null, now);
        room.AddParticipant(Guid.NewGuid(), now);

        var chatRoomRepo = new Mock<IChatRoomRepository>();
        chatRoomRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        var service = CreateService(chatRoomRepo, out _, out _, out _, new FakeDateTimeProvider(now));
        var request = new SendMessageRequest
        {
            RoomId = room.Id,
            SenderId = Guid.NewGuid(),
            Type = MessageType.Text,
            Content = "hello"
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.SendMessageAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendMessageAsync_CreatesStatuses_AndPublishesEvent()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var senderId = Guid.NewGuid();
        var recipient1 = Guid.NewGuid();
        var recipient2 = Guid.NewGuid();
        var room = new ChatRoom(false, null, now);
        room.AddParticipant(senderId, now);
        room.AddParticipant(recipient1, now);
        room.AddParticipant(recipient2, now);

        var chatRoomRepo = new Mock<IChatRoomRepository>();
        chatRoomRepo.Setup(r => r.GetByIdAsync(room.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        Message? savedMessage = null;
        var messageRepo = new Mock<IMessageRepository>();
        messageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => savedMessage = message)
            .Returns(Task.CompletedTask);
        messageRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        IReadOnlyCollection<MessageStatus>? savedStatuses = null;
        var statusRepo = new Mock<IMessageStatusRepository>();
        statusRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<MessageStatus>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<MessageStatus>, CancellationToken>((statuses, _) => savedStatuses = statuses.ToArray())
            .Returns(Task.CompletedTask);

        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<MessageSentEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(chatRoomRepo, messageRepo, statusRepo, publisher, new FakeDateTimeProvider(now));

        var request = new SendMessageRequest
        {
            RoomId = room.Id,
            SenderId = senderId,
            Type = MessageType.Text,
            Content = "hello"
        };

        var result = await service.SendMessageAsync(request, CancellationToken.None);

        Assert.NotNull(savedMessage);
        Assert.NotNull(savedStatuses);
        Assert.Equal(2, savedStatuses!.Count);
        Assert.All(savedStatuses, status => Assert.Equal(MessageDeliveryStatus.Sent, status.Status));
        Assert.Contains(savedStatuses, status => status.RecipientId == recipient1);
        Assert.Contains(savedStatuses, status => status.RecipientId == recipient2);
        Assert.Equal(savedMessage!.Id, result.Id);

        publisher.Verify(p => p.PublishAsync(It.Is<MessageSentEvent>(
            evt => evt.MessageId == savedMessage.Id
                   && evt.RoomId == room.Id
                   && evt.SenderId == senderId
                   && evt.Content == "hello"
                   && evt.RecipientIds.Length == 2
                   && evt.RecipientIds.Contains(recipient1)
                   && evt.RecipientIds.Contains(recipient2)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessagesAsync_Throws_WhenPageNumberInvalid()
    {
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            new FakeDateTimeProvider(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetMessagesAsync(Guid.NewGuid(), 0, 20, CancellationToken.None));
    }

    [Fact]
    public async Task GetMessagesAsync_Throws_WhenPageSizeInvalid()
    {
        var service = CreateService(
            out _,
            out _,
            out _,
            out _,
            new FakeDateTimeProvider(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetMessagesAsync(Guid.NewGuid(), 1, 101, CancellationToken.None));
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsMappedPage()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var room = new ChatRoom(false, null, now);
        room.AddParticipant(Guid.NewGuid(), now);

        var chatRoomRepo = new Mock<IChatRoomRepository>();
        chatRoomRepo.Setup(r => r.GetByIdAsync(room.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        var message1 = new Message(room.Id, Guid.NewGuid(), MessageType.Text, "one", now);
        message1.AddStatus(Guid.NewGuid(), MessageDeliveryStatus.Sent, now);
        var message2 = new Message(room.Id, Guid.NewGuid(), MessageType.Text, "two", now.AddMinutes(1));

        var page = new PagedResult<Message>
        {
            Items = new[] { message1, message2 },
            PageNumber = 1,
            PageSize = 2,
            HasNext = false
        };

        var messageRepo = new Mock<IMessageRepository>();
        messageRepo.Setup(r => r.GetByRoomIdPagedAsync(room.Id, 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var statusRepo = new Mock<IMessageStatusRepository>();
        var publisher = new Mock<IEventPublisher>();
        var service = CreateService(chatRoomRepo, messageRepo, statusRepo, publisher, new FakeDateTimeProvider(now));

        var result = await service.GetMessagesAsync(room.Id, 1, 2, CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(room.Id, result.Items.First().RoomId);
    }

    [Fact]
    public async Task MarkMessageReadAsync_AddsStatus_WhenMissing()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var message = new Message(Guid.NewGuid(), Guid.NewGuid(), MessageType.Text, "hello", now);
        var room = new ChatRoom(false, null, now);
        room.AddParticipant(message.SenderId, now);

        var chatRoomRepo = new Mock<IChatRoomRepository>();
        chatRoomRepo.Setup(r => r.GetByIdAsync(message.ChatRoomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        var messageRepo = new Mock<IMessageRepository>();
        messageRepo.Setup(r => r.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var statusRepo = new Mock<IMessageStatusRepository>();
        statusRepo.Setup(r => r.GetAsync(message.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageStatus?)null);
        statusRepo.Setup(r => r.AddAsync(It.IsAny<MessageStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        statusRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<MessageReadEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(chatRoomRepo, messageRepo, statusRepo, publisher, new FakeDateTimeProvider(now));

        var result = await service.MarkMessageReadAsync(message.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(MessageDeliveryStatus.Read, result.Status);
        publisher.Verify(p => p.PublishAsync(It.IsAny<MessageReadEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkMessageReadAsync_UpdatesStatus_WhenExisting()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var message = new Message(Guid.NewGuid(), Guid.NewGuid(), MessageType.Text, "hello", now);
        var room = new ChatRoom(false, null, now);
        room.AddParticipant(message.SenderId, now);

        var chatRoomRepo = new Mock<IChatRoomRepository>();
        chatRoomRepo.Setup(r => r.GetByIdAsync(message.ChatRoomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        var messageRepo = new Mock<IMessageRepository>();
        messageRepo.Setup(r => r.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var existingStatus = new MessageStatus(message.Id, Guid.NewGuid(), MessageDeliveryStatus.Sent, now.AddMinutes(-5));
        var statusRepo = new Mock<IMessageStatusRepository>();
        statusRepo.Setup(r => r.GetAsync(message.Id, existingStatus.RecipientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingStatus);
        statusRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<MessageReadEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(chatRoomRepo, messageRepo, statusRepo, publisher, new FakeDateTimeProvider(now));

        var result = await service.MarkMessageReadAsync(message.Id, existingStatus.RecipientId, CancellationToken.None);

        Assert.Equal(MessageDeliveryStatus.Read, result.Status);
        Assert.Equal(MessageDeliveryStatus.Read, existingStatus.Status);
        publisher.Verify(p => p.PublishAsync(It.IsAny<MessageReadEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MessageService CreateService(
        out Mock<IChatRoomRepository> chatRoomRepository,
        out Mock<IMessageRepository> messageRepository,
        out Mock<IMessageStatusRepository> statusRepository,
        out Mock<IEventPublisher> eventPublisher,
        FakeDateTimeProvider dateTimeProvider)
    {
        chatRoomRepository = new Mock<IChatRoomRepository>();
        messageRepository = new Mock<IMessageRepository>();
        statusRepository = new Mock<IMessageStatusRepository>();
        eventPublisher = new Mock<IEventPublisher>();

        return new MessageService(
            chatRoomRepository.Object,
            messageRepository.Object,
            statusRepository.Object,
            eventPublisher.Object,
            dateTimeProvider);
    }

    private static MessageService CreateService(
        Mock<IChatRoomRepository> chatRoomRepository,
        out Mock<IMessageRepository> messageRepository,
        out Mock<IMessageStatusRepository> statusRepository,
        out Mock<IEventPublisher> eventPublisher,
        FakeDateTimeProvider dateTimeProvider)
    {
        messageRepository = new Mock<IMessageRepository>();
        statusRepository = new Mock<IMessageStatusRepository>();
        eventPublisher = new Mock<IEventPublisher>();

        return new MessageService(
            chatRoomRepository.Object,
            messageRepository.Object,
            statusRepository.Object,
            eventPublisher.Object,
            dateTimeProvider);
    }

    private static MessageService CreateService(
        Mock<IChatRoomRepository> chatRoomRepository,
        Mock<IMessageRepository> messageRepository,
        Mock<IMessageStatusRepository> statusRepository,
        Mock<IEventPublisher> eventPublisher,
        FakeDateTimeProvider dateTimeProvider)
    {
        return new MessageService(
            chatRoomRepository.Object,
            messageRepository.Object,
            statusRepository.Object,
            eventPublisher.Object,
            dateTimeProvider);
    }
}
