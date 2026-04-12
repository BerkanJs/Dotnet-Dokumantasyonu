namespace OcpDemo;

// Yeni indirim tipi = yeni class → mevcut koda dokunma
// bunu yazmasaydık → her indirim tipi aynı class'a if/switch olarak eklenirdi
public interface IIndirimStrategy
{
    decimal Hesapla(decimal fiyat);
    string Ad { get; }
}
