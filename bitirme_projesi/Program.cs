using bitirme_projesi.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization; // 🔹 Bunu en üste ekle, yoksa hata verir

var builder = WebApplication.CreateBuilder(args);

// 🔹 CORS (React için izinler)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// 🔹 Controller + JSON döngü engelleme ayarı
builder.Services.AddControllers()
    .AddJsonOptions(x =>
        x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// 🔹 PostgreSQL bağlantısı
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.UseVector()));

// 🔹 Gemini AI service
builder.Services.AddScoped<bitirme_projesi.Services.GeminiService>();

builder.Services.AddHttpClient<bitirme_projesi.Services.HuggingFaceEmbeddingService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 🔹 Swagger sadece geliştirme ortamında aktif
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");

app.UseStaticFiles(); // ✅ Bu olmazsa resimler görünmez

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
