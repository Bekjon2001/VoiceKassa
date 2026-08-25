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

// --- Application services / DI wiring ---
<<<<<<< HEAD
builder.Services.AddScoped<ISaleRepository, SaleRepository>();
builder.Services.AddScoped<IShopRepository, ShopRepository>();
builder.Services.AddScoped<IAiExtractionService, GeminiExtractionService>();
builder.Services.AddScoped<IAiQueryService, GeminiQueryService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<QueryService>();
builder.Services.AddScoped<ShopService>();
=======
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IAiExtractionService, GeminiExtractionService>();
builder.Services.AddScoped<IAiQueryService, GeminiQueryService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<QueryService>();
builder.Services.AddScoped<BusinessService>();
>>>>>>> main

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(opt =>
{
    // Loosened for local development against the browser demo /
    // future mobile app. Lock this down to real origins before production.
    opt.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
