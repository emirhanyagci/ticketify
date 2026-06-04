using System.ComponentModel.DataAnnotations;

namespace Ticketify.ViewModels;

public class TicketCreateViewModel
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
    [Display(Name = "Başlık")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Açıklama zorunludur.")]
    [MaxLength(2000, ErrorMessage = "Açıklama en fazla 2000 karakter olabilir.")]
    [Display(Name = "Açıklama")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Öncelik seçiniz.")]
    [Display(Name = "Öncelik")]
    public string Priority { get; set; } = "Medium";

    [Required(ErrorMessage = "Departman seçiniz.")]
    [Display(Name = "Departman")]
    public string Department { get; set; } = "General";

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
