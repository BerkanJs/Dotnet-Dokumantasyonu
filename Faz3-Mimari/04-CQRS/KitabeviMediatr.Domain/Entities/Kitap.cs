using KitabeviMediatr.Domain.Exceptions;
using KitabeviMediatr.Domain.ValueObjects;

namespace KitabeviMediatr.Domain.Entities;

public class Kitap
{
    public int Id { get; private set; }
    public string Baslik { get; private set; } = null!;
    public Isbn Isbn { get; private set; } = null!;
    public Fiyat Fiyat { get; private set; } = null!;
    public int StokAdedi { get; private set; }
    public bool Aktif { get; private set; } = true;
    //           ↑ varsayılan true: yeni eklenen kitap aktif başlar
    //             bunu yazmasaydık → AktifKitaplarSpecification bu alana erişemezdi
    //             soft-delete alternatifi — fiziksel silmek yerine Aktif = false

    protected Kitap() { }

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

    public void StokAzalt(int adet)
    {
        if (adet <= 0)
            throw new DomainException("Azaltılacak adet sıfırdan büyük olmalı");
        if (adet > StokAdedi)
            throw new DomainException($"Yetersiz stok. Mevcut: {StokAdedi}, İstenen: {adet}");

        StokAdedi -= adet;
    }

    public void FiyatGuncelle(Fiyat yeniFiyat) => Fiyat = yeniFiyat;

    public void Deaktive()
    {
        Aktif = false;
        //      ↑ soft-delete: DB'den silmek yerine görünmez yap
        //        bunu yazmasaydık → Aktif = false dışarıdan direkt set edilemez (private set)
        //        siparişlerde referans bütünlüğü korunur — fiziksel silme FK hatasına yol açardı
    }
}
