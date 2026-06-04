using Ticketify.Models;

namespace Ticketify.ViewModels;

public class AdminDashboardViewModel
{
    public IEnumerable<Ticket> Tickets { get; set; } = Enumerable.Empty<Ticket>();
    public string? StatusFilter { get; set; }
    public string? DepartmentFilter { get; set; }
    public string? Search { get; set; }
    public string? PriorityFilter { get; set; }
    public string? AssigneeFilter { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public IEnumerable<User> Employees { get; set; } = Enumerable.Empty<User>();
    public Dictionary<int, User> SuggestedAssignees { get; set; } = new();
    public Dictionary<int, int> EmployeeActiveTicketCounts { get; set; } = new();

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

    public static readonly Dictionary<string, string> PriorityOptions = new()
    {
        { "Low", "Düşük" },
        { "Medium", "Orta" },
        { "High", "Yüksek" }
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
