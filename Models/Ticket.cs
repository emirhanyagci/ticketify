namespace Ticketify.Models;

public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";  // Low, Medium, High
    public string Status { get; set; } = "Open";       // Open, InProgress, Resolved

    // Departman (ticket'ın ait olduğu alan)
    public string Department { get; set; } = "General"; // General, Software, Hardware, Network, Other

    // Reporter (Bildiren kişi)
    public int UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string? UserAvatarUrl { get; set; }

    // Assignee (Atanan çalışan)
    public int? AssigneeId { get; set; }
    public string? AssigneeEmail { get; set; }
    public string? AssigneeFullName { get; set; }
    public string? AssigneeAvatarUrl { get; set; }

    // Atamayı yapan kişi
    public int? AssignedById { get; set; }
    public string? AssignedByEmail { get; set; }
    public string? AssignedByFullName { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedAt { get; set; }
    public DateTime? InProgressAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
