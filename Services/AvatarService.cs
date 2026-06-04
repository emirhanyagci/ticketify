namespace Ticketify.Services;

public class AvatarService
{
    // DiceBear API v9 — Her kullanıcıya e-posta seed'i ile tutarlı avatar
    // Stil rastgele seçilir ama seed'e göre aynı kalır
    private static readonly string[] Styles =
    {
        "avataaars", "bottts", "lorelei", "notionists", "thumbs",
        "pixel-art", "adventurer", "big-smile", "croodles"
    };

    /// <summary>
    /// Verilen seed (genellikle e-posta) için DiceBear API avatar URL'i üretir.
    /// Seed değişmediği sürece URL de değişmez → tutarlı profil fotoğrafı.
    /// </summary>
    public string GenerateAvatarUrl(string seed)
    {
        // Stili seed hash'ine göre belirle (deterministik)
        var styleIndex = Math.Abs(seed.GetHashCode()) % Styles.Length;
        var style = Styles[styleIndex];
        return $"https://api.dicebear.com/9.x/{style}/svg?seed={Uri.EscapeDataString(seed)}&backgroundColor=b6e3f4,c0aede,d1d4f9,ffd5dc,ffdfbf";
    }
}
