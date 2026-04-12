namespace CreationalDemo.Factory;

public interface IKitapExporter
{
    void Disa_Aktar(List<string> kitaplar);
}

public class PdfExporter : IKitapExporter
{
    public void Disa_Aktar(List<string> kitaplar)
        => Console.WriteLine($"[PDF] {kitaplar.Count} kitap PDF'e aktarıldı.");
}

public class ExcelExporter : IKitapExporter
{
    public void Disa_Aktar(List<string> kitaplar)
        => Console.WriteLine($"[Excel] {kitaplar.Count} kitap Excel'e aktarıldı.");
}
