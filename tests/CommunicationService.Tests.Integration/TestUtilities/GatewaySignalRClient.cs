using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace CommunicationService.Tests.Integration.TestUtilities;

public sealed class GatewaySignalRClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private TaskCompletionSource<MessageDto>? _receiveMessage;
    private TaskCompletionSource<MessageStatusDto>? _messageRead;

    public GatewaySignalRClient(Uri chatBaseUri)
    {
        var hubUri = new Uri(chatBaseUri, "hubs/chat");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
            })
            .Build();

        _connection.On<MessageDto>("ReceiveMessage", message =>
        {
            _receiveMessage?.TrySetResult(message);
        });

        _connection.On<MessageStatusDto>("MessageRead", status =>
        {
            _messageRead?.TrySetResult(status);
        });
    }

    public Task ConnectAsync()
    {
        return _connection.StartAsync();
    }

    public Task JoinRoomAsync(Guid roomId, Guid userId)
    {
        return _connection.InvokeAsync("JoinRoom", roomId, userId);
    }

    public Task SendMessageAsync(SendMessageRequest request)
    {
        return _connection.InvokeAsync("SendMessage", request);
    }

    public Task MarkReadAsync(Guid messageId, Guid readerId)
    {
        return _connection.InvokeAsync("MarkRead", messageId, readerId);
    }

    public Task<MessageDto> ExpectReceiveMessageAsync(TimeSpan timeout)
    {
        _receiveMessage = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _receiveMessage.Task.WaitAsync(timeout);
    }

    public Task<MessageStatusDto> ExpectMessageReadAsync(TimeSpan timeout)
    {
        _messageRead = new TaskCompletionSource<MessageStatusDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _messageRead.Task.WaitAsync(timeout);
    }

    public ValueTask DisposeAsync()
    {
        return _connection.DisposeAsync();
    }
}
