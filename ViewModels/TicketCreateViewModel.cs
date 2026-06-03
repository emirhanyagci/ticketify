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

    public static readonly Dictionary<string, string> PriorityOptions = new()
    {
        { "Low", "Düşük" },
        { "Medium", "Orta" },
        { "High", "Yüksek" }
    };
}
