// SRP Demo

var sifreServisi  = new SrpDemo.SifreServisi();
var validator     = new SrpDemo.KullaniciValidator();
var emailServisi  = new SrpDemo.EmailServisi();
var kullaniciServ = new SrpDemo.KullaniciServisi(sifreServisi, validator, emailServisi);

kullaniciServ.KayitOl("berkan@example.com", "gizlisifre123");
