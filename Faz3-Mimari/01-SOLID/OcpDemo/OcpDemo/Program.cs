using OcpDemo;

var servis = new FiyatServisi();
decimal kitapFiyati = 150m;

// Yeni indirim tipi eklemek için FiyatServisi'ne DOKUNMADIK
servis.IndirimliFiyatHesapla(kitapFiyati, new OgrenciIndirimi());
servis.IndirimliFiyatHesapla(kitapFiyati, new YazMevsimIndirimi());
servis.IndirimliFiyatHesapla(kitapFiyati, new KuponIndirimi(0.85m));  // %15 kupon
