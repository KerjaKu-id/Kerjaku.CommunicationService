using System;
using System.Linq;

namespace CommunicationService.Domain.Entities;

public class UserShadow
{
    private UserShadow()
    {
    }

    public UserShadow(
        Guid id,
        string email,
        string? displayName,
        string? avatarUrl,
        string? firebaseUid,
        string? role,
        string? status,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
        FirebaseUid = firebaseUid;
        Role = role;
        Status = status;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? FirebaseUid { get; private set; }
    public string? Role { get; private set; }
    public string? Status { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Formatted name derived from Email local part if DisplayName is not set.
    /// Delimiters like dots, underscores, and dashes are replaced with spaces,
    /// and each word is capitalized for readability.
    /// </summary>
    public string FormattedName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return DisplayName;
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                return "User";
            }

            var index = Email.IndexOf('@');
            if (index <= 0)
            {
                return Email;
            }

            var localPart = Email.Substring(0, index);
            var parts = localPart.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var capitalizedParts = parts.Select(p =>
                string.IsNullOrEmpty(p) ? p : char.ToUpper(p[0]) + p.Substring(1).ToLower()
            );

            return string.Join(" ", capitalizedParts);
        }
    }

    public void UpdateProfile(string? displayName, string? avatarUrl, DateTimeOffset updatedAt)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName;
        }

        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            AvatarUrl = avatarUrl;
        }

        UpdatedAt = updatedAt;
    }

    public void UpdateRole(string? role, DateTimeOffset updatedAt)
    {
        if (!string.IsNullOrWhiteSpace(role))
        {
            Role = role;
        }

        UpdatedAt = updatedAt;
    }

    public void UpdateStatus(string? status, DateTimeOffset updatedAt)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            Status = status;
        }

        UpdatedAt = updatedAt;
    }

    public void UpdateEmail(string email, DateTimeOffset updatedAt)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = email;
        }

        UpdatedAt = updatedAt;
    }
}
