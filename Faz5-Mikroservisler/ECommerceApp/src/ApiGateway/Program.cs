var builder = WebApplication.CreateBuilder(args);

// YARP config — Gün 122'de işlendi, Gün 125'te servisler eklenince dolacak
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.MapReverseProxy();
app.Run();
