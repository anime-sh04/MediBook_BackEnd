namespace MediBook.Notification.API.Entities;

/// <summary>Valid values for Notification.Type.</summary>
public static class NotificationTypes
{
    public const string Booking      = "BOOKING";
    public const string Reminder     = "REMINDER";
    public const string Cancellation = "CANCELLATION";
    public const string Payment      = "PAYMENT";
    public const string FollowUp     = "FOLLOWUP";
}

/// <summary>Valid values for Notification.Channel.</summary>
public static class NotificationChannels
{
    public const string App   = "APP";
    public const string Email = "EMAIL";
    public const string Sms   = "SMS";   // defined for schema completeness; not dispatched
}
