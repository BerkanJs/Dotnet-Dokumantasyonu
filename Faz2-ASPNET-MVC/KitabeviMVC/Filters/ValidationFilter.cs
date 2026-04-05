using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KitabeviMVC.Filters;

// ─────────────────────────────────────────────────────────────────────
// Gün 19 — IActionFilter (Sync versiyon)
//
// Her POST action'da tekrar eden şu kodu ortadan kaldırır:
//
//   if (!ModelState.IsValid)
//       return View(model);
//
// Bu filter controller seviyesinde [TypeFilter] ile ekleniyor.
// Yani sadece KitapController'daki action'lara uygulanır — global değil.
//
// Neden global değil?
//   Bazı action'lar (örn. JSON API action'ları) View() değil BadRequest()
//   döndürmek ister. Controller tipine bakarak ayırt ediyoruz.
// ─────────────────────────────────────────────────────────────────────
public class ValidationFilter : IActionFilter
{
    // OnActionExecuting → action çalışmadan ÖNCE tetiklenir.
    // context.ModelState.IsValid false ise action'ı hiç çalıştırmıyoruz.
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Sadece POST, PUT, PATCH gibi body olan isteklerde kontrol et.
        // GET istekleri için model binding olmaz — IsValid zaten true gelir.
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsDelete(method))
            return;

        if (context.ModelState.IsValid)
            return; // Geçerliyse dokunma, action normal çalışsın

        // ModelState geçersiz — action'ı durdurup View'ı geri döndür.

        // context.ActionArguments: action'ın parametrelerini isim → değer olarak tutar.
        // Örnek: { "model" → KitapFormViewModel { Baslik = "", ... } }
        // View'a modeli geri göndermemiz gerekiyor ki hata mesajları görünsün.
        var model = context.ActionArguments.Values
            .FirstOrDefault(v => v is not null && !v.GetType().IsPrimitive && v is not string);

        // context.Controller → şu anki controller instance'ı (object tipinde gelir).
        // "as Controller" → Controller base class'a cast et.
        // "as" operatörü: cast başarısız olursa exception fırlatmaz, null döner.
        if (context.Controller is Controller controller)
        {
            // context.Result'ı set etmek pipeline'ı kısa devre yapar:
            // action metodu çalışmaz, OnActionExecuted de tetiklenmez,
            // direkt Result execute edilir.
            context.Result = controller.View(model);
        }
    }

    // OnActionExecuted → action çalıştıktan SONRA tetiklenir.
    // Validation kısa devre yaptıysa burası çalışmaz.
    // Başka bir şey yapmıyoruz — boş bırakıyoruz.
    public void OnActionExecuted(ActionExecutedContext context) { }
}
