namespace AutoParts.Api.Domain;

public class ChatMessage
{
    public int Id { get; set; }
    public int UserId { get; set; }   // Who conversation belongs to
    public int SenderUserId { get; set; } // Who sent the message
    public bool IsAdmin { get; set; }     // true = admin, false = user
    public string Message { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

