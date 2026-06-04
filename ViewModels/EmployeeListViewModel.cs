using Ticketify.Models;

namespace Ticketify.ViewModels;

public class EmployeeListViewModel
{
    public IEnumerable<User> Employees { get; set; } = Enumerable.Empty<User>();
    public string? DepartmentFilter { get; set; }
    public int TotalCount { get; set; }

    public static readonly Dictionary<string, string> DepartmentOptions = new()
    {
        { "General", "Genel" },
        { "Software", "Yazılım" },
        { "Hardware", "Donanım" },
        { "Network", "Ağ / Altyapı" },
        { "Other", "Diğer" }
    };
}
