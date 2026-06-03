using Ticketify.Models;

namespace Ticketify.ViewModels;

public class AdminDashboardViewModel
{
    public IEnumerable<Ticket> Tickets { get; set; } = Enumerable.Empty<Ticket>();
    public string? StatusFilter { get; set; }

    // İstatistikler
    public int TotalCount { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }

    public static readonly Dictionary<string, string> StatusOptions = new()
    {
        { "Open", "Açık" },
        { "InProgress", "İşlemde" },
        { "Resolved", "Çözüldü" }
    };
}
