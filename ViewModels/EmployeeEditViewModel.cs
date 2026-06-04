using System.ComponentModel.DataAnnotations;

namespace Ticketify.ViewModels;

public class EmployeeEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad Soyad zorunludur.")]
    [MaxLength(100, ErrorMessage = "Ad Soyad en fazla 100 karakter olabilir.")]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Yeni Şifre (boş bırakırsanız değişmez)")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    public string? NewPassword { get; set; }

    [Required(ErrorMessage = "Departman seçiniz.")]
    [Display(Name = "Departman")]
    public string Department { get; set; } = "General";

    public static readonly Dictionary<string, string> DepartmentOptions = new()
    {
        { "General", "Genel" },
        { "Software", "Yazılım" },
        { "Hardware", "Donanım" },
        { "Network", "Ağ / Altyapı" },
        { "Other", "Diğer" }
    };
}
