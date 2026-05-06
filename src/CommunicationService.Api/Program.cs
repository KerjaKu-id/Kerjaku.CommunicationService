using CommunicationService.Api.Hubs;
using CommunicationService.Api.Middleware;
using CommunicationService.Application.Options;
using CommunicationService.Application.Services;
using CommunicationService.Infrastructure;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.Configure<TemporaryChatOptions>(builder.Configuration.GetSection("TemporaryChat"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<TemporaryChatOptions>>().Value);
builder.Services.AddScoped<IChatRoomService, ChatRoomService>();
builder.Services.AddScoped<IMessageService, MessageService>();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

public partial class Program
{
}
