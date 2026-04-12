# Gün 55 — Behavioral Patterns 2: Mediator, State, Iterator

---

## 1. Mediator

### Günlük hayat

Hava trafik kontrol kulesi düşün. Uçaklar birbirleriyle konuşmuyor — hepsi kuleyle konuşuyor. Kule koordinasyonu yapıyor. Uçak sayısı arttıkça kule olmadan haberleşme imkânsız hale gelir.

### Faz2'de böyle yaptık

`Faz2-ASPNET-MVC/KitabeviMVC` Gün 35'te MediatR ile CQRS uyguladın. MediatR tam Mediator pattern:

```csharp
// Controller sadece _mediator biliyor — handler'ları bilmiyor
var result = await _mediator.Send(new KitapEkleCommand(model));
// MediatR doğru handler'ı buluyor: KitapEkleHandler
// Controller handler'a direkt bağımlı değil (DIP)
```

Handler sayısı 20'ye çıksa bile Controller değişmiyor — sadece `_mediator.Send(...)`.

### Büyük projede böyle yapmalısın

```csharp
// Mediator/KitapMediatorDemo.cs

// Request nesneleri — sadece veri taşır (Command pattern ile örtüşür)
public record KitapEkleKomut(string Baslik, decimal Fiyat);
public record KitapListesorgu();

// Handler'lar — asıl işi yapan
public class KitapEkleHandler
{
    public int Handle(KitapEkleKomut komut) { ... }
}

// Mediator: hangi handler çağrılacak kararını veriyor
// Controller sadece mediator'ı biliyor — handler'ları bilmiyor
public class KitapMediator
{
    public int Gonder(KitapEkleKomut komut)    => _ekleHandler.Handle(komut);
    public List<string> Gonder(KitapListesorgu sorgu) => _listeHandler.Handle(sorgu);
    // gerçek MediatR: DI container handler'ları reflection ile buluyor
    // bunu yazmasaydık → Controller tüm handler'ları inject etmek zorunda kalırdı
}
```

### MediatR ile fark

Manuel Mediator'da `Gonder` overload'ları yazmak zorundasın. MediatR'da DI container reflection ile handler'ı otomatik buluyor — sen sadece `_mediator.Send(request)` yazıyorsun.

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **5 altı handler, basit akış** | Direkt servis inject et | — |
| **10+ use case, pipeline behavior** | ✅ MediatR değer katıyor | ✅ Şart: logging, validation, auth pipeline |
| **Overengineering** | CRUD-only uygulamada MediatR ceremony fazla | — |

---

## 2. State

### Günlük hayat

Trafik lambası: kırmızıyken "geç" dersen geçemezsin. Yeşilken "dur" dersen durursun. Sarıyken "hızlan" dersen hata. Her renk farklı davranış — ama hepsi aynı lambadan geliyor.

### Faz2'de böyle yaptık

Faz2'de sipariş durumu string olarak tutuldu:

```csharp
// Model: public string Durum { get; set; } = "Bekleme";
// Servis:
if (siparis.Durum == "Bekleme") { ... }
else if (siparis.Durum == "Hazırlanıyor") { ... }
// Her yeni durum bu if zincirini büyütüyor
// "Kargoya verildi" eklenince tüm if blokları güncelleniyor
```

500 kullanıcıda 3 durum varsa çalışır. 50k'da durum sayısı 6-7'ye çıkınca yönetilemez.

### Büyük projede böyle yapmalısın

```csharp
// State/SiparisDurumu.cs

public interface ISiparisDurumu
{
    void OdemeAl(Siparis siparis);
    void Gonder(Siparis siparis);
    void Teslim(Siparis siparis);
    string Ad { get; }
}

// Her durum kendi class'ında — geçersiz işlemler burada reddediliyor
public class BeklemeDurumu : ISiparisDurumu
{
    public void OdemeAl(Siparis siparis)
    {
        Console.WriteLine("[ÖDEME] Alındı");
        siparis.Durum = new HazirlaniyorDurumu();
        // durum geçişi burada — Siparis class'ına if eklenmedi
        // bunu yazmasaydık → Siparis.OdemeAl() içinde if (durum == X) olurdu
    }

    public void Gonder(Siparis siparis) => Console.WriteLine("[HATA] Önce ödeme");
    // geçersiz işlem: hata ver, state'i değiştirme
}

public class Siparis
{
    public ISiparisDurumu Durum { get; set; } = new BeklemeDurumu();

    public void OdemeAl() => Durum.OdemeAl(this);
    public void Gonder()   => Durum.Gonder(this);
    // hangi durumda ne olacağını bilmiyor — Durum class'ı biliyor
}
```

Yeni durum eklemek (örn. "İade Bekliyor"):

```csharp
// Yeni class yaz — Siparis ve diğer durumlara dokunma (OCP)
public class IadeBekliyorDurumu : ISiparisDurumu { ... }
```

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **2-3 durum, nadiren değişir** | String + if yeterli | — |
| **4+ durum, sık geçiş, iş kuralları** | ✅ State pattern | ✅ Şart — yeni durum = yeni class |
| **Overengineering** | Toggle (açık/kapalı) için State | — |

---

## 3. Iterator

### Günlük hayat

Kütüphanedeki kitap kataloğu. Raflar farklı düzenlenmiş — ama sen "bir sonraki kitabı ver" diyorsun, rafın yapısını bilmiyorsun. Katalog sana sırayla kitapları veriyor.

### Faz2'de böyle yaptık

`IQueryable<T>` zinciri Iterator pattern — DB'den satırlar `foreach` ile teker teker gelir, tamamı bellekte birikmez:

```csharp
// Faz2: EfKitapServisi
await _context.Kitaplar
    .Where(k => k.Kategori == kategori)
    .Select(k => new KitapListeViewModel { ... })
    .ToListAsync();
    // ToListAsync(): iterator'ı tüketir, listeye dönüştürür
    // .AsAsyncEnumerable() olsaydı → satır satır streaming
```

### Büyük projede böyle yapmalısın

```csharp
// Iterator/KitapIterator.cs

// yield return: lazy evaluation — tüm liste bellekte tutulmaz
public IEnumerable<string> PahaliBas(decimal limitFiyat)
{
    foreach (var kitap in _kitaplar)
    {
        if (kitap.Fiyat >= limitFiyat)
            yield return kitap;
            // bunu yazmasaydık → önce tüm liste filtrelenip bellekte tutulurdu
            // yield ile: caller her eleman isteyince bir sonraki üretilir
    }
}

// IAsyncEnumerable<T>: async streaming — büyük veri setinde satır satır işle
public async IAsyncEnumerable<string> StreamKitaplar()
{
    foreach (var kitap in _kitaplar)
    {
        await Task.Delay(10);   // DB I/O simülasyonu
        yield return kitap;
        // bunu yazmasaydık → 100k satır önce bellekte toplanırdı
        // streaming ile: caller her satırı alır almaz işleyebilir
    }
}
```

Kullanım:

```csharp
// IEnumerable — foreach ile lazy
foreach (var kitap in koleksiyon.PahaliBas(100))
    Console.WriteLine(kitap);

// IAsyncEnumerable — await foreach ile async streaming
await foreach (var kitap in koleksiyon.StreamKitaplar())
    await response.WriteAsync(kitap);
```

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **100 altı kayıt** | ToList() yeterli | ToList() yeterli |
| **10k+ kayıt, stream/export** | ✅ yield return veya IAsyncEnumerable | ✅ Şart — bellek patlaması önlenir |
| **Gerçek zamanlı akış (SSE/gRPC)** | Nadiren gerek | ✅ IAsyncEnumerable ideal |

---

## 4. Visitor ve Memento (Kısa)

**Visitor** — operasyonu veri yapısından ayır. Farklı nesne türleri üzerinde farklı işlemler yapmak istiyorsun ama nesnelere dokunmak istemiyorsun. Örnek: AST (Abstract Syntax Tree) üzerinde farklı dönüşümler. Günlük .NET kodunda nadir.

**Memento** — nesnenin anlık görüntüsünü al, ilerleyen zamanda geri yükle. Undo/redo için. Command pattern ile birlikte sıkça kullanılır. Örnek: text editörde ctrl+z.

---

## Bu Haftanın Pattern'leri ve Faz3 Bağlantısı

```
Mediator  → Faz3 CQRS: _mediator.Send(command) — handler'lar birbirini bilmez
State     → Domain model: Siparis.Durum geçişleri iş kuralıyla yönetilir
Iterator  → IQueryable streaming, büyük veri export, SignalR streaming
```

---

## Sorular

1. Mediator pattern olmadan 15 handler'ı Controller'a inject etsen ne olurdu?
2. Faz2'deki `if (siparis.Durum == "Bekleme")` yaklaşımı ile State pattern'in test edilebilirlik farkı ne?
3. `IEnumerable<T>` ile `IAsyncEnumerable<T>` arasında ne zaman hangisini seçersin?
