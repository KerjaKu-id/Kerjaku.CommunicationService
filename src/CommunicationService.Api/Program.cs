using CommunicationService.Api.Hubs;
using CommunicationService.Api.Middleware;
using CommunicationService.Application.Options;
using CommunicationService.Application.Services;
using CommunicationService.Infrastructure;
using CommunicationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.Configure<TemporaryChatOptions>(builder.Configuration.GetSection("TemporaryChat"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<TemporaryChatOptions>>().Value);
builder.Services.AddScoped<IChatRoomService, ChatRoomService>();
builder.Services.AddScoped<IMessageService, MessageService>();

builder.Services.AddInfrastructure(builder.Configuration);

// CORS is handled centrally by the Nginx API Gateway.
// Do NOT add UseCors() here — it will cause duplicate Access-Control-Allow-Origin headers.

var app = builder.Build();

// Apply pending EF Core migrations on startup.
// This is critical when database is deleted/recreated via docker volumes.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();
    
    var maxRetries = 10;
    var delay      = TimeSpan.FromSeconds(5);

    for (int i = 1; i <= maxRetries; i++)
    {
        try
        {
            Console.WriteLine($"[Migration] CommunicationService: Attempt {i}/{maxRetries}...");
            await db.Database.MigrateAsync();
            Console.WriteLine("[Migration] CommunicationService: Success!");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Migration] CommunicationService: Failed: {ex.Message}");
            if (i == maxRetries) throw;
            await Task.Delay(delay);
        }
    }
}

// CORS handled by Nginx gateway — no UseCors() here.

app.UseMiddleware<ExceptionHandlingMiddleware>();

// app.UseHttpsRedirection();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

public partial class Program
{
}
