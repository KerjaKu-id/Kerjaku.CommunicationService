# Communication Service

Production-ready Communication Service for one-to-one chat with clean architecture and a foundation for future group chat support.

## Features
- One-to-one chat with participant-based design
- Temporary chats with expiration
- Text and image messages (URL-only for images)
- Message status tracking: sent, delivered, read
- Real-time messaging via SignalR
- Event publishing via RabbitMQ (MessageSent, MessageRead, ChatExpired)
- Pagination for message retrieval

## Architecture
- **Domain**: Core entities and enums
- **Application**: DTOs, interfaces, services, options, and exceptions
- **Infrastructure**: EF Core, repositories, RabbitMQ publisher, background worker
- **API**: Controllers, HATEOAS, SignalR hub, middleware

## Runtime Components
- **SignalR hub**: real-time message and read notifications
- **Chat expiration worker**: marks expired temporary chats
- **RabbitMQ publisher**: emits integration events on message sent/read and chat expired

## Requirements
- .NET SDK 8.0
- SQL Server (local or Docker)
- RabbitMQ (local or Docker) for event publishing

## Configuration
See `src/CommunicationService.Api/appsettings.json` and `appsettings.Development.json`.

Key settings:
- `ConnectionStrings:CommunicationDb`
- `RabbitMq:*`
- `TemporaryChat:DefaultTtlHours`
- `ChatExpirationWorker:CheckIntervalSeconds`

## Database
Tables:
- `chat_rooms`
- `chat_participants`
- `messages`
- `message_status`

### Migrations
Initial migration lives under `src/CommunicationService.Infrastructure/Data/Migrations`.

Create migration (if you add new schema changes):
```bash
dotnet ef migrations add <Name> \
  --project src/CommunicationService.Infrastructure/CommunicationService.Infrastructure.csproj \
  --startup-project src/CommunicationService.Api/CommunicationService.Api.csproj \
  --output-dir Data/Migrations
```

Apply migrations:
```bash
dotnet ef database update \
  --project src/CommunicationService.Infrastructure/CommunicationService.Infrastructure.csproj \
  --startup-project src/CommunicationService.Api/CommunicationService.Api.csproj
```

## API Endpoints (HATEOAS)
- `POST /chat/rooms`
- `GET /chat/rooms/{id}`
- `POST /chat/messages`
- `GET /chat/messages?roomId=`

Responses include `self`, `send_message`, and `get_messages` links.

### Pagination
`GET /chat/messages` supports:
- `pageNumber` (default 1)
- `pageSize` (default 20)

The response uses a `PagedResult` envelope with `items` and `hasNext`.

## SignalR Hub
Hub path: `/hubs/chat`

Methods:
- `JoinRoom(roomId, userId)`
- `SendMessage(request)`
- `MarkRead(messageId, readerId)`

Client events:
- `ReceiveMessage`
- `MessageRead`

## Events Published
- `MessageSentEvent`
- `MessageReadEvent`
- `ChatExpiredEvent`

`MessageSentEvent` includes `Content` and `RecipientIds` to allow the Notification Service to trigger recipient notifications without additional lookups.

## Run with Docker
```bash
docker compose up --build
```

API will be available at `http://localhost:8080`.

## Run Locally
```bash
dotnet build CommunicationService.slnx
dotnet run --project src/CommunicationService.Api/CommunicationService.Api.csproj
```

Default local URL (launch profile): `http://localhost:5116`.

## Troubleshooting
- **Login failed for user 'sa'**: run the EF migration update command to create the database.
- **SQL Server not reachable**: verify `ConnectionStrings:CommunicationDb` points to your SQL Server host.
- **RabbitMQ unavailable**: the API will start and log connection failures until the broker is reachable.