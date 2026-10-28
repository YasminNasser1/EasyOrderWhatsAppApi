using EasyOrderWhatsAppApi.Services;

var builder = WebApplication.CreateBuilder(args);

// قراءة البورت من السيرفر السحابي (Render / Railway / Koyeb) أو استخدام 5055 محلياً
var port = Environment.GetEnvironmentVariable("PORT") ?? "5055";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers();
builder.Services.AddHttpClient<WhatsAppService>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/whatsapp"));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));
app.MapControllers();

app.Run();
