namespace SrpDemo;

// Demo için minimal model — gerçek projede ayrı dosyada olur
public class Kullanici
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string SifreHash { get; set; } = string.Empty;
}
