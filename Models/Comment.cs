namespace Ticketify.Models;

public class Comment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int AuthorId { get; set; }
    public string AuthorEmail { get; set; } = string.Empty;
    public string AuthorFullName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string AuthorRole { get; set; } = string.Empty; // Employee veya Admin
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
