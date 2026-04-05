using System.Security.Claims;
using KitabeviMVC.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace KitabeviMVC.Authorization;

// Gün 20: Resource-based authorization.
//
// Senaryo: Bir kitabı sadece onu ekleyen kullanıcı düzenleyebilir.
// Admin her kitabı düzenleyebilir.
//
// Bu kural [Authorize(Roles = "...")] ile ifade edilemez çünkü
// hangi kaydın kimin olduğunu attribute bilemez — veritabanına bakmak gerekir.
// IAuthorizationHandler bu kontrolü kayıt üzerinde yapar.

// "Requirement" → policy'nin neye ihtiyaç duyduğunu tanımlayan işaretçi sınıf.
// İçi boş olabilir — sadece "bu kuralı bu handler işleyecek" bağlantısını kurar.
public class KitapDuzenlemeRequirement : IAuthorizationRequirement { }

// AuthorizationHandler<TRequirement, TResource>
//   TRequirement → hangi requirement bu handler işliyor?
//   TResource    → hangi kayıt üzerinde çalışıyor?
public class KitapDuzenlemeHandler
    : AuthorizationHandler<KitapDuzenlemeRequirement, KitapFormViewModel>
{
    // "HandleRequirementAsync" → IAuthorizationService.AuthorizeAsync çağrılınca tetiklenir.
    // context.User    → giriş yapmış kullanıcı (ClaimsPrincipal)
    // requirement     → KitapDuzenlemeRequirement instance'ı
    // resource        → controller'dan geçilen kayıt (KitapFormViewModel)
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        KitapDuzenlemeRequirement requirement,
        KitapFormViewModel resource)
    {
        // Admin her kitabı düzenleyebilir — kayıt kime ait olursa olsun
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Admin değilse: kitabı kim ekledi?
        // ClaimTypes.NameIdentifier → kullanıcının id'si (burada email olarak saklıyoruz)
        var kullaniciId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // resource.EkleyenKullanici → kitabın sahibi (gerçek projede DB'den gelir)
        // Eşleşiyorsa geçer
        if (resource.EkleyenKullanici == kullaniciId)
            context.Succeed(requirement);

        // Succeed çağırılmadıysa: yetki yok → controller'da Forbid() devreye girer

        return Task.CompletedTask;
    }
}
