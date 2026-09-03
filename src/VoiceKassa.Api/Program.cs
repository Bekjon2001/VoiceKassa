using Microsoft.EntityFrameworkCore;
using VoiceKassa.AiServices;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Application.Services;
using VoiceKassa.DataLayer;
using VoiceKassa.DataLayer.Repository;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default topilmadi (appsettings.json yoki user-secrets'ni tekshiring).");

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

// --- Gemini AI client (bepul tarif) ---
var geminiOptions = new GeminiApiOptions
{
    ApiKey = builder.Configuration["Gemini:ApiKey"]
        ?? throw new InvalidOperationException("Gemini:ApiKey topilmadi. `dotnet user-secrets set \"Gemini:ApiKey\" \"...\"` orqali qo'shing. Kalitni https://aistudio.google.com/apikey sahifasidan bepul olish mumkin."),
    Model = builder.Configuration["Gemini:Model"] ?? "gemini-2.0-flash",
};
builder.Services.AddSingleton(geminiOptions);
builder.Services.AddHttpClient<GeminiApiClient>();
builder.Services.AddSingleton<EdgeTtsService>();

// --- Application services / DI wiring ---
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAiExtractionService, GeminiExtractionService>();
builder.Services.AddScoped<IAiQueryService, GeminiQueryService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<QueryService>();
builder.Services.AddScoped<BusinessService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Frontend papkasini ham shu serverdan beramiz: localhost:55983/admin.html
// yoki shunchaki localhost:55983 (index.html) brauzerda ochiladi.
// `dotnet run` da ContentRoot = src/VoiceKassa.Api, DLL ni to'g'ridan-to'g'ri
// ishga tushirganda esa ildiz papka bo'lishi mumkin — ikkalasini ham tekshiramiz.
var frontendDir = new[]
    {
        Path.Combine(builder.Environment.ContentRootPath, "..", "..", "frontend"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "frontend"),
    }
    .Select(Path.GetFullPath)
    .FirstOrDefault(Directory.Exists);

builder.Services.AddCors(opt =>
{
    // Loosened for local development against the browser demo /
    // future mobile app. Lock this down to real origins before production.
    opt.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Swagger har doim ochiq (MVP bosqichi, oson ko'rib chiqish uchun).
app.UseSwagger();
app.UseSwaggerUI();

if (frontendDir is not null)
{
    var frontendProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendDir);
    // "/" manzili index.html'ni ochadi
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendProvider });
    // admin.html, super-admin.html, css, js va h.k.
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendProvider });
    // "/frontend/index.html" manzillari ham xuddi shu papkani ko'rsatadi.
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendProvider, RequestPath = "/frontend" });
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// === So'rov vaqti va marshrutini log qilish ===
// Har bir HTTP so'rov uchun: method, path, controller/action, status, vaqt.
// Log: "[12:54:30] GET /Query/SpeakSuper/speak-super → 200 (1.23 s)"
app.Use(async (ctx, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();

    var endpoint = ctx.GetEndpoint();
    var route = endpoint?.DisplayName ?? "(unknown)";
    // DisplayName "VoiceKassa.Api.Controllers.QueryController.SpeakSuper (VoiceKassa.Api)" kabi —
    // qisqartiramiz: faqat Controller.Action.
    var shortRoute = route.Split(' ').FirstOrDefault() ?? route;
    // So'rov ID (token) — tokenning birinchi 6 belgisi, foydalanuvchini kuzatish uchun
    var token = ctx.Request.Headers["X-Super-Admin-Token"].FirstOrDefault();
    var tokenShort = string.IsNullOrEmpty(token) ? "-" : (token.Length > 6 ? token[..6] + "…" : token);

    app.Logger.LogInformation(
        "[{Time}] {Method} {Path} → {Status} ({Ms:0.00} s) [token={Token}] {Route}",
        DateTime.Now.ToString("HH:mm:ss.fff"),
        ctx.Request.Method,
        ctx.Request.Path.Value,
        ctx.Response.StatusCode,
        sw.Elapsed.TotalSeconds,
        tokenShort,
        shortRoute);
});

app.Run();
