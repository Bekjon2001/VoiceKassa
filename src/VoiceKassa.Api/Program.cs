using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Net;
using VoiceKassa.AiServices;
using Microsoft.AspNetCore.Diagnostics;
using VoiceKassa.Api;
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
    Model = builder.Configuration["Gemini:Model"] ?? "gemini-3.6-flash",
};
builder.Services.AddSingleton(geminiOptions);

// --- Sozlamalar (Setup kalitlari) ---
builder.Services.Configure<SetupKeyOptions>(builder.Configuration.GetSection("Setup:Keys"));

var setup = builder.Configuration.GetSection("Setup:Keys").Get<SetupKeyOptions>();
// Development uchun konfiguratsiyada kalit bo'lmasa, lokal standart kalit ishlatiladi.
builder.Services.Configure<SetupKeyOptions>(options =>
{
    options.FirstSuperAdminKey = setup?.FirstSuperAdminKey
        ?? (builder.Environment.IsDevelopment() ? "dev-first-admin-key-2026" : null);
});
builder.Services.AddHttpClient<GeminiApiClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<EdgeTtsService>();

// Jarvis suhbat xotirasi — Singleton: server qayta ishga tushmagan ekan
// har bir sessiyaning so'nggi almashinuvlari xotirada saqlanadi.
builder.Services.AddSingleton<JarvisConversationMemory>();

// --- Application services / DI wiring ---
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IAiExtractionService, GeminiExtractionService>();
builder.Services.AddScoped<IAiQueryService, GeminiQueryService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<QueryService>();
builder.Services.AddScoped<BusinessService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ReservationService>();
// Bron kodi bilan kirish (enter) cheklovi — singleton: butun ilova bo'ylab bitta hisob.
builder.Services.AddSingleton<ReservationEnterLimiter>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Ishonchli reverse proxy (nginx, IIS, k8s ingress) orqali kelgan so'rovlar
    // uchun haqiqiy client IP-ni X-Forwarded-For / X-Forwarded-Proto dan olamiz.
    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("127.0.0.1"), 32));
});

builder.Services.AddSingleton<AuthLimiter>();
builder.Services.AddProblemDetails();
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

app.UseForwardedHeaders();

// Global xato boshqaruvi: barcha isnot tushgan xatolar ProblemDetails (RFC 7807)
// ko'rinishida qaytariladi, stack trace mijozga berilmaydi.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async ctx =>
    {
        var exHandlerPathFeature = ctx.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exHandlerPathFeature?.Error;
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Title = exception is not null ? exception.Message : "Xato yuz berdi.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = ctx.Request.Path,
        };
        ctx.Response.StatusCode = problem.Status.Value;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(problem);
    });
});

if (builder.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

// === So'rov vaqti va marshrutini log qilish ===
// Har bir HTTP so'rov uchun: method, path, controller/action, status, vaqt.
// Log: "[12:54:30] GET /Query/SpeakSuper/speak-super → 200 (1.23 s)"
// Eslatma: bu UseRouting'dan keyin, endpoint ishga tushishidan oldin bo'lishi
// shart — MapControllers'dan keyin yozilsa hech qachon ishlamaydi.
app.UseRouting();
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

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
