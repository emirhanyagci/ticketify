using Ticketify.Repositories;
using Ticketify.Services;
using Ticketify.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

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
builder.Services.AddSingleton<IUserRepository, JsonUserRepository>();
builder.Services.AddSingleton<ITicketRepository, JsonTicketRepository>();

// Auth Service
builder.Services.AddSingleton<AuthService>();

var app = builder.Build();

// Error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

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

    // users.json — seed admin kullanıcı
    var usersFile = Path.Combine(dataDir, "users.json");
    if (!File.Exists(usersFile))
    {
        var authService = app.Services.GetRequiredService<AuthService>();
        var adminUser = new User
        {
            Id = 1,
            Email = "admin@ticketify.com",
            PasswordHash = authService.HashPassword("Admin123!"),
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            new List<User> { adminUser },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(usersFile, json);

        Console.WriteLine("✅ Admin kullanıcı oluşturuldu: admin@ticketify.com / Admin123!");
    }
}
