using Ticketify.Models;

namespace Ticketify.ViewModels;

public class TicketListViewModel
{
    public IEnumerable<Ticket> Tickets { get; set; } = Enumerable.Empty<Ticket>();
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string UserRole { get; set; } = "User";
}
