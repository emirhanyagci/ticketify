using Ticketify.Models;

namespace Ticketify.ViewModels;

public class TicketDetailViewModel
{
    public Ticket Ticket { get; set; } = null!;
    public IEnumerable<Comment> Comments { get; set; } = Enumerable.Empty<Comment>();
    public IEnumerable<User> AvailableEmployees { get; set; } = Enumerable.Empty<User>();
    public List<TimelineEvent> Timeline { get; set; } = new();

    public bool CanComment { get; set; }       // Employee veya Admin
    public bool CanAssign { get; set; }        // Sadece Admin
    public bool CanUpdateStatus { get; set; }  // Sadece Admin
}

public class TimelineEvent
{
    public string Icon { get; set; } = string.Empty;
    public string IconColor { get; set; } = "#6366f1";
    public string Label { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Detail { get; set; }
}
