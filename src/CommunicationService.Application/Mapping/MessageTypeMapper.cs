using CommunicationService.Domain.Enums;

namespace CommunicationService.Application.Mapping;

public static class MessageTypeMapper
{
    public static MessageType FromApiValue(string? value, MessageType fallback = MessageType.Text)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "text" => MessageType.Text,
            "image" => MessageType.Image,
            "invoice_card" => MessageType.InvoiceCard,
            "system" => MessageType.System,
            _ => fallback
        };
    }

    public static string ToApiValue(MessageType type)
    {
        return type switch
        {
            MessageType.Text => "text",
            MessageType.Image => "image",
            MessageType.InvoiceCard => "invoice_card",
            MessageType.System => "system",
            _ => "text"
        };
    }
}
