using Ticketify.Models;

namespace Ticketify.ViewModels;

public class AdminDashboardViewModel
{
    public IEnumerable<Ticket> Tickets { get; set; } = Enumerable.Empty<Ticket>();
    public string? StatusFilter { get; set; }
    public string? DepartmentFilter { get; set; }
    public IEnumerable<User> Employees { get; set; } = Enumerable.Empty<User>();

    // İstatistikler
    public int TotalCount { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }
    public int UnassignedCount { get; set; }

    public static readonly Dictionary<string, string> StatusOptions = new()
    {
        { "Open", "Açık" },
        { "InProgress", "İşlemde" },
        { "Resolved", "Çözüldü" }
    };

    public static readonly Dictionary<string, string> DepartmentOptions = new()
    {
        { "General", "Genel" },
        { "Software", "Yazılım" },
        { "Hardware", "Donanım" },
        { "Network", "Ağ / Altyapı" },
        { "Other", "Diğer" }
    };
}
