# Gün 1 — CLR Nedir? JVM ile Karşılaştırma

---

## 1. CLR Nedir?

C# yazdığında bilgisayarın işlemcisi bunu doğrudan okuyamaz. İşlemci sadece 0 ve 1'lerden oluşan makine kodunu anlar.

**CLR (Common Language Runtime)**, C# kodunun çalışabilmesi için gereken ortamdır. Aynı Java'daki JVM gibi düşün — aralarındaki farkları ileride göreceğiz.

CLR'nin temel işleri:
- Kodunu makine koduna çevirmek
- Belleği yönetmek (kullanılmayan nesneleri silmek)
- Hataları yakalamak (try/catch altyapısı)

---

## 2. Kod Çalışmadan Önce Ne Olur?

Yazdığın C# kodu çalışmadan önce şu yoldan geçer:

```
1. C# kaynak kodu  →  sen bunu yazarsın
2. IL kodu         →  derleyici (Roslyn) bunu üretir, .dll dosyasının içindedir
3. Makine kodu     →  CLR bunu çalışma anında üretir ve işlemci çalıştırır
```

**IL (Intermediate Language)** nedir?

Roslyn derleyicisi C# kodunu doğrudan makine koduna çevirmez. Önce "IL" adı verilen ara bir dile çevirir. Bu IL kodu hiçbir işletim sistemine veya işlemciye özgü değildir.

Sonra CLR bu IL'yi alır ve "şu an çalıştığım Windows mu, Linux mu? x64 işlemci mi, ARM mı?" diye bakarak o platforma uygun makine kodu üretir.

**Bu neden önemli?** Aynı .dll dosyasını Windows'ta da, Linux'ta da, Mac'te de çalıştırabilirsin. Tek kaynak kod, her platform.

---

## 3. Somut Örnek — Tarif ve Aşçı

Düşün ki uluslararası bir tarif kitabı var. Tarifler evrensel bir dilde yazılmış: "ekmeği kes, eti ızgara yap, sos koy."

- **Sen** → tarifi yazan (C# kodu)
- **Tarif kitabı** → IL kodu (.dll içinde)
- **Aşçı** → CLR (tarifi alır, o mutfağın ekipmanıyla pişirir)
- **Mutfak** → işletim sistemi + işlemci (Windows/Linux, x64/ARM)

İstanbul'daki mutfak da, Berlin'deki mutfak da aynı tarifi kullanır. Aşçı tarifi kendi mutfağına göre uygular. Tarifi değiştirmene gerek yok.

---

## 4. JVM ile Karşılaştırma

Java da aynı mantıkla çalışır. Peki fark ne?

**Benzer olanlar:**
- İkisi de kaynak kodu → ara dil → makine kodu yolunu izler
- İkisi de belleği otomatik yönetir
- İkisi de platform bağımsızlık sağlar

**Farklar:**

| | JVM | CLR |
|---|---|---|
| Ara dil adı | Bytecode | IL (Intermediate Language) |
| Kimler kullanır? | Java, Kotlin, Scala | C#, F#, VB.NET |
| Dil esnekliği | Diller birbirine benzer | Diller birbirinden çok farklı olabilir |

CLR'nin "gerçek multi-language" olması şu anlama gelir: C# ile F# tamamen farklı dil paradigmaları (nesne yönelimli vs fonksiyonel), ama ikisi de aynı IL'ye derlenir ve birbirinin kütüphanelerini kullanabilir. Java ekosisteminde bu kadar derin bir fark yok.

---

## 5. Managed ve Unmanaged Kod

**Managed kod:** CLR'nin yönettiği kod. C# yazdığında bu managed koddur. CLR belleği takip eder, hataları yönetir.

**Unmanaged kod:** CLR'nin dışında yaşayan kod. C veya C++ ile yazılmış, işletim sistemi veya donanıma yakın çalışan kod.

Neden önemli? Bazen CLR'nin dışına çıkmak gerekir. Örneğin:

- Windows'un kendi API'leri C ile yazılmış → unmanaged
- Kriptografi kütüphaneleri, veritabanı sürücüleri → genellikle native (unmanaged)
- Gömülü sistem veya donanım ile iletişim

C# bunu **P/Invoke** mekanizmasıyla yapar — managed C# kodundan unmanaged bir fonksiyon çağırırsın. Kodda şöyle görünür:

```csharp
[DllImport("kernel32.dll")]
static extern uint GetTickCount();
```

`kernel32.dll` Windows'un kendi kütüphanesi, CLR'nin dışında. Bu satır CLR'ye "bu fonksiyon dışarıda, oradan çağır" diyor.

Web API yazarken bunu doğrudan kullanmassın. Ama kullandığın kütüphanelerin içinde bu var.

---

## 6. JIT mi, AOT mu?

Az önce "CLR IL'yi çalışma anında makine koduna çevirir" dedik. Bu **JIT (Just-in-Time)** derleme.

"Çalışma anında" ne demek? Program çalışmaya başladıktan sonra, o kod parçası ilk kez çağrıldığında derlenir.

**JIT'in avantajı:** Çalıştığı platforma göre optimize edebilir.  
**JIT'in dezavantajı:** İlk çalışmada bir "ısınma" süresi var. ASP.NET uygulamasına gelen ilk istek biraz daha yavaş olur — sebep bu.

---

**.NET 7 ile gelen alternatif: AOT (Ahead-of-Time)**

AOT'ta derleme sırasında makine kodu üretilir. Program çalışmaya başladığında her şey hazır, ısınma süresi yok.

```bash
dotnet publish -r win-x64 -p:PublishAot=true
```

| | JIT | AOT |
|---|---|---|
| Ne zaman derlenir? | Çalışma anında | Derleme sırasında |
| İlk başlangıç | Biraz yavaş | Çok hızlı |
| Dosya boyutu | Küçük (IL taşır) | Büyük (makine kodu taşır) |
| Esneklik | Yüksek | Kısıtlı |

**AOT ne zaman kullanılır?**  
Çok hızlı başlaması gereken yerlerde: AWS Lambda, Azure Functions, CLI araçları. Sunucuda sürekli çalışan bir web API için genellikle JIT yeterlidir.

---

## 7. AppDomain → AssemblyLoadContext

Bu konu biraz daha teknik, genel fikri alman yeterli.

**.NET Framework döneminde** `AppDomain` vardı. Tek bir process içinde birden fazla izole uygulama çalıştırmak için kullanılırdı. Ağır ve karmaşıktı.

**.NET Core ile** bu kaldırıldı, yerine `AssemblyLoadContext` (ALC) geldi.

**Pratik kullanımı:** Plugin sistemi yaparsan. Her plugin kendi izole ortamında yüklenir → birbirinin DLL'leriyle çakışmaz. Sıradan web API geliştirmede buna dokunmassın.

---

## 8. Web Geliştirmede Nerede Görünür?

CLR arka planda çalışır, görmezsin. Ama etkileri şuralardan hissedilir:

- **İlk istek yavaşlığı:** JIT henüz derlemedi → ısınma süreci → Kubernetes'te readiness probe beklenir
- **Native AOT:** Mikroservis container boyutunu küçültmek için tercih edilir
- **P/Invoke:** Kullandığın JSON, kriptografi, veritabanı kütüphanelerinin altında vardır

---

## 9. Kontrol Soruları

1. C# kodu işlemciye ulaşmadan önce hangi aşamalardan geçer? Her aşamada ne olur?

ilk derlenir (IL) sonra CLI derlenen kodu makine kodu yapar 

2. IL kodu neden "platform bağımsız"? Makine kodu neden bağımsız değil?

IL kodu derler CLI sonra kodu isletim sistemine göre uyarlar bu yüzden bagımsızdır Makine kodunun bagımsız olmaması da makinenin işletim sisteminden kaynaklı 

3. Managed ve unmanaged kod arasındaki fark nedir? Bir web API yazarken neden unmanaged kodla işin olsun?

Managed kod CLI'de kullanılan kod unmanaged kod makine koduna yakın c c++ kodları bunları isletim sistemi kullanır bir api dısarıdan windowsun bir kodunu kullanmak isteyebilir
kernel32.dll mesela dısarıdan gelen unmanged bir koddur CLI'in unmanaged ve managed kodları birlikte kullanması onemli

4. JIT ile AOT arasındaki temel trade-off nedir? Serverless bir fonksiyon için hangisi daha uygun, neden?

JIT just in time AOT ahead of time JIT eger fonksiyona ihtiyaç varsa kullanılmadan önce onu derler AOTda direkt uygulama calısınca tüm fonnksiyonlar derlenir
aws gibi direkt ayağa kalkması gereken sistemlerde AOT daha iyi kullanıslı JIT'in trade off bazen fonksiyon cagrıldıgında ufak bekleme süresi olabiliyor JIT dinamik AOT statik
kod degistirilmiyor serverless fonksiyonlar cok kısa sürede sık sık calısır aot bunda daha iyi kod zaten basta derleniyor ama aotta da kullanılmayan kodlar da derlenir

5. Java ile C#'ın çalışma mantığı benzer. Peki CLR'nin "multi-language" desteği JVM'den nasıl farklı?

javada jvm ve bytecoding var ve dillerin birbirine cok yakın olması lazım CLR'de bu kısıtlama yok F# ve C# dillerini cok uzak olmasına ragmen derler