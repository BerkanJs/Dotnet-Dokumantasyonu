namespace CreationalDemo.Factory;

// Factory Method: nesne oluşturma kararı tek yerde
// Yeni format eklenince sadece burası değişir — çağıran kod dokunulmaz (OCP)
public static class ExporterFactory
{
    public static IKitapExporter Olustur(string format) => format switch
    {
        "pdf"   => new PdfExporter(),
        "excel" => new ExcelExporter(),
        // yeni format: buraya bir satır ekle, başka hiçbir yere dokunma
        _ => throw new ArgumentException($"Bilinmeyen format: {format}")
    };
    // bunu yazmasaydık → her çağıran kendi switch'ini yazardı → OCP ihlali
}
