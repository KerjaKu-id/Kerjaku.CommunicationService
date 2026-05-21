namespace CommunicationService.Domain.Enums;

/// <summary>
/// User roles in the chat system.
/// Synced from Identity Service - same values used across all services.
/// 
/// Design: Values are hardcoded to match Identity Service enum.
/// If Identity Service changes roles, must update here and all services.
/// 
/// Used to determine:
/// - Permissions in chat rooms (who can post, edit, delete, moderate)
/// - Visibility of admin features
/// - Notification routing
/// - SLA response times (CS gets higher priority)
/// </summary>
public enum UserRole
{
    /// <summary>Platform administrator. Full access to all chats and controls.</summary>
    Admin = 1,
    
    /// <summary>End customer. Can initiate support chats and participate in job-based chats.</summary>
    Customer = 2,
    
    /// <summary>Partner/vendor. Can offer services and chat with customers and admin.</summary>
    Partner = 3,
    
    /// <summary>Customer Service representative. Can handle support tickets and escalations.</summary>
    CS = 4,
}
