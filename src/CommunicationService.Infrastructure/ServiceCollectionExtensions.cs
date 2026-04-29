using CommunicationService.Application.Abstractions.Events;
using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Application.Abstractions.Time;
using CommunicationService.Infrastructure.Data;
using CommunicationService.Infrastructure.Events;
using CommunicationService.Infrastructure.Repositories;
using CommunicationService.Infrastructure.Services;
using CommunicationService.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunicationService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CommunicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("CommunicationDb")));

        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.Configure<ChatExpirationWorkerOptions>(configuration.GetSection("ChatExpirationWorker"));

        services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageStatusRepository, MessageStatusRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        services.AddHostedService<ChatExpirationWorker>();

        return services;
    }
}
