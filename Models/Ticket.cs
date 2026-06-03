namespace Ticketify.Models;

public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";  // Low, Medium, High
    public string Status { get; set; } = "Open";       // Open, InProgress, Resolved
    public int UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty; // Denormalize: admin listelemede join gerekmez
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
