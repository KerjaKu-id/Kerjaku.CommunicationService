using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunicationService.Domain.Entities;
using CommunicationService.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Api.Controllers;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Content,
    string Type,
    string? ReferenceId,
    bool IsRead,
    DateTimeOffset CreatedAt
);

public record CreateNotificationRequest(
    Guid UserId,
    string Title,
    string Content,
    string Type,
    string? ReferenceId
);

[ApiController]
[Route("notifications")]
public class NotificationsController : ControllerBase
{
    private readonly CommunicationDbContext _db;

    public NotificationsController(CommunicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications([FromQuery] Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest("UserId is required.");
        }

        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(
                n.Id,
                n.UserId,
                n.Title,
                n.Content,
                n.Type,
                n.ReferenceId,
                n.IsRead,
                n.CreatedAt
            ))
            .ToListAsync(ct);

        return Ok(notifications);
    }

    [HttpPost]
    public async Task<ActionResult<NotificationDto>> CreateNotification([FromBody] CreateNotificationRequest request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("UserId, Title, and Content are required.");
        }

        var notification = new Notification(
            request.UserId,
            request.Title,
            request.Content,
            request.Type,
            request.ReferenceId
        );

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        var dto = new NotificationDto(
            notification.Id,
            notification.UserId,
            notification.Title,
            notification.Content,
            notification.Type,
            notification.ReferenceId,
            notification.IsRead,
            notification.CreatedAt
        );

        return CreatedAtAction(nameof(GetNotifications), new { userId = notification.UserId }, dto);
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (notification == null)
        {
            return NotFound();
        }

        notification.MarkAsRead();
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
