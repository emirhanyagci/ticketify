using Ticketify.Models;

namespace Ticketify.ViewModels;

public class TicketListViewModel
{
    public IEnumerable<Ticket> Tickets { get; set; } = Enumerable.Empty<Ticket>();
    public string UserEmail { get; set; } = string.Empty;
}
