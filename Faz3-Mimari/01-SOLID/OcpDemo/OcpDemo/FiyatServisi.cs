namespace OcpDemo;

// FiyatServisi indirim TİPİNİ bilmiyor — sadece interface'i biliyor
// Yeni indirim eklenince bu class AÇILMIYOR
// bunu yazmasaydık → her yeni indirim tipi buraya if/switch olarak eklenirdi
public class FiyatServisi
{
    public decimal IndirimliFiyatHesapla(decimal fiyat, IIndirimStrategy indirim)
    {
        var sonuc = indirim.Hesapla(fiyat);
        // bunu yazmasaydık → hangi indirim uygulandığını bilemezdik

        Console.WriteLine($"{indirim.Ad}: {fiyat:C2} → {sonuc:C2}");
        return sonuc;
    }
}
