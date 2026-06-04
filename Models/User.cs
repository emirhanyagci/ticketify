namespace Ticketify.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // "Admin", "User", "Employee"
    public string? Department { get; set; }   // Sadece Employee için: General, Software, Hardware, Network, Other
    public string? AvatarUrl { get; set; }    // DiceBear profil fotoğrafı URL
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
