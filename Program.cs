using Microsoft.OpenApi;
using Ticketify.Repositories;
using Ticketify.Services;
using Ticketify.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// API Controllers (Swagger için)
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ticketify API",
        Version = "v1",
        Description = "Ticketify destek talebi yönetim sistemi REST API'si"
    });
});

// Cookie Authentication
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

builder.Services.AddAuthorization();

// Repository Pattern — Singleton (thread-safe via SemaphoreSlim)
// dependency injection kismi
builder.Services.AddSingleton<IUserRepository, JsonUserRepository>();
builder.Services.AddSingleton<ITicketRepository, JsonTicketRepository>();
builder.Services.AddSingleton<ICommentRepository, JsonCommentRepository>();

// Services
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<AvatarService>();

var app = builder.Build();

// Error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Swagger UI (sadece development'ta)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ticketify API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Ticketify API Dokümantasyonu";
        c.DefaultModelsExpandDepth(-1); // Model şemalarını kapalı başlat
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); // API route'ları ([Route("api/...")] ile)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --- Seed Data & App_Data klasörü ---
await SeedDataAsync(app);

app.Run();

// ----- Seed Helper -----
static async Task SeedDataAsync(WebApplication app)
{
    var env = app.Services.GetRequiredService<IWebHostEnvironment>();
    var dataDir = Path.Combine(env.ContentRootPath, "App_Data");

    // App_Data klasörünü oluştur
    if (!Directory.Exists(dataDir))
        Directory.CreateDirectory(dataDir);

    // tickets.json oluştur
    var ticketsFile = Path.Combine(dataDir, "tickets.json");
    if (!File.Exists(ticketsFile))
        await File.WriteAllTextAsync(ticketsFile, "[]");

    // comments.json oluştur
    var commentsFile = Path.Combine(dataDir, "comments.json");
    if (!File.Exists(commentsFile))
        await File.WriteAllTextAsync(commentsFile, "[]");

    // users.json — seed admin kullanıcı
    var usersFile = Path.Combine(dataDir, "users.json");
    if (!File.Exists(usersFile))
    {
        var authService = app.Services.GetRequiredService<AuthService>();
        var avatarService = app.Services.GetRequiredService<AvatarService>();

        var adminUser = new User
        {
            Id = 1,
            FullName = "Sistem Yöneticisi",
            Email = "admin@ticketify.com",
            PasswordHash = authService.HashPassword("Admin123!"),
            Role = "Admin",
            AvatarUrl = avatarService.GenerateAvatarUrl("admin@ticketify.com"),
            CreatedAt = DateTime.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            new List<User> { adminUser },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(usersFile, json);

        Console.WriteLine("✅ Admin kullanıcı oluşturuldu: admin@ticketify.com / Admin123!");
    }
    else
    {
        // Mevcut kullanıcıları güncelle: FullName veya AvatarUrl yoksa tamamla
        var usersJson = await File.ReadAllTextAsync(usersFile);
        var users = System.Text.Json.JsonSerializer.Deserialize<List<User>>(
            usersJson,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) ?? new List<User>();

        var avatarService = app.Services.GetRequiredService<AvatarService>();
        bool changed = false;

        foreach (var u in users)
        {
            if (string.IsNullOrEmpty(u.AvatarUrl))
            {
                u.AvatarUrl = avatarService.GenerateAvatarUrl(u.Email);
                changed = true;
            }
            if (string.IsNullOrEmpty(u.FullName))
            {
                u.FullName = u.Email.Split('@')[0];
                changed = true;
            }
        }

        if (changed)
        {
            var updatedJson = System.Text.Json.JsonSerializer.Serialize(
                users,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(usersFile, updatedJson);
            Console.WriteLine("✅ Mevcut kullanıcılar güncellendi (avatar/fullname).");
        }
    }
}
