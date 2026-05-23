var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();

// TODO Gün 126+: EF Core, MassTransit eklenecek

app.Run();
