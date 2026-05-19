using KitabeviOnion.Domain.Exceptions;
using KitabeviOnion.Domain.ValueObjects;

namespace KitabeviOnion.Domain.Entities;

public class Kitap
{
    public int Id { get; private set; }
    //              ↑ private set: dışarıdan Id değiştirilemez
    //                bunu yazmasaydık → başka kod kitap.Id = 999 yapabilirdi

    public string Baslik { get; private set; } = null!;
    public Isbn Isbn { get; private set; } = null!;
    //          ↑ string değil Isbn Value Object — format kuralı otomatik geliyor
    //            bunu yazmasaydık → geçersiz ISBN DB'ye girebilirdi

    public Fiyat Fiyat { get; private set; } = null!;
    //           ↑ decimal değil Fiyat Value Object — negatif fiyat engelleniyor
    //             bunu yazmasaydık → fiyat.KdvEkle() metodu olmazdı

    public int StokAdedi { get; private set; }
    //                     ↑ private set: stok değişikliği sadece metodlar üzerinden

    // EF Core için protected constructor (dışarıdan new Kitap() kapatıyoruz)
    protected Kitap() { }
    //         ↑ bunu yazmasaydık → EF Core reflection ile nesne oluşturamazdı

    public Kitap(string baslik, Isbn isbn, Fiyat fiyat, int ilkStok)
    {
        if (string.IsNullOrWhiteSpace(baslik))
            throw new DomainException("Başlık boş olamaz");

        if (ilkStok < 0)
            throw new DomainException("Stok negatif olamaz");

        Baslik = baslik;
        Isbn = isbn;
        Fiyat = fiyat;
        StokAdedi = ilkStok;
    }

    public bool StokVarMi() => StokAdedi > 0;
    //  ↑ iş kuralı Domain'de — Application veya Controller yazmak zorunda değil
    //    bunu yazmasaydık → if (kitap.StokAdedi > 0) her yerde tekrar yazılırdı

    public void StokAzalt(int adet)
    {
        if (adet <= 0)
            throw new DomainException("Azaltılacak adet sıfırdan büyük olmalı");

        if (adet > StokAdedi)
            throw new DomainException($"Yetersiz stok. Mevcut: {StokAdedi}, İstenen: {adet}");
        //  ↑ invariant: stok negatife düşemez — bu kural Kitap'ın sorumluluğu
        //    bunu yazmasaydık → stok kontrolü her sipariş servisinde tekrar yazılırdı

        StokAdedi -= adet;
    }

    public void FiyatGuncelle(Fiyat yeniFiyat)
    {
        Fiyat = yeniFiyat;
        // Fiyat Value Object — yeni Fiyat nesnesi atanıyor, eski kayboluyor
    }
}
