using Microsoft.EntityFrameworkCore;
using Stepify.Models.Db;
using Microsoft.AspNetCore.Authentication.Cookies; // 1. เพิ่มบรรทัดนี้

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<StepifyContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    );

// 2. เพิ่มการตั้งค่า Authentication ตรงนี้ (ก่อน builder.Build())
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // ถ้ายังไม่ล็อกอิน ให้เด้งมาหน้านี้
        options.AccessDeniedPath = "/Auth/Login"; // ถ้าสิทธิ์ไม่พอ ก็ให้เด้งมาหน้านี้
        options.ExpireTimeSpan = TimeSpan.FromDays(1); // ล็อกอินค้างไว้ 1 วัน
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication(); // 3. สำคัญมาก! ต้องใส่คำสั่งนี้ก่อน UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
