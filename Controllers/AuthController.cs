using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Stepify.Controllers
{
    public class AuthController : Controller
    {
        private readonly StepifyContext _db;

        public AuthController(StepifyContext db)
        {
            _db = db;
        }

        // ==========================================
        // 1. หน้า Register (GET & POST)
        // ==========================================
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(User data)
        {
            // เช็คว่ามี Email นี้ในระบบหรือยัง
            var checkEmail = _db.Users.FirstOrDefault(u => u.Email == data.Email);
            if (checkEmail != null)
            {
                ViewBag.Error = "อีเมลนี้ถูกใช้งานแล้ว กรุณาใช้อีเมลอื่น";
                return View(data);
            }

            var newUser = new User
            {
                Username = data.Username,
                Email = data.Email,
                Tel = data.Tel,
                Password = data.Password, // *ในระบบจริงควรเข้ารหัส (Hash) ก่อนบันทึก
                Role = "User" // สมัครใหม่หน้าเว็บ ให้เป็น User ธรรมดาเสมอ
            };

            _db.Users.Add(newUser);
            _db.SaveChanges();

            return RedirectToAction("Login");
        }

        // ==========================================
        // 2. หน้า Login (GET & POST)
        // ==========================================
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            // ค้นหา User จาก Email และ Password (เหมือนที่คุณเคยทำใน Lab)
            var user = _db.Users.FirstOrDefault(u => u.Email == Email && u.Password == Password);

            if (user != null)
            {
                // สร้างข้อมูลประจำตัว (Claims) เพื่อเก็บลง Cookie
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username ?? ""),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Role, user.Role ?? "User"), // เอาไว้แยกสิทธิ์ Admin
                    new Claim("UserId", user.UserId.ToString())
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                // สั่ง Sign in (สร้าง Cookie)
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                // เช็ค Role ถ้าเป็น Admin ให้ไปหน้า Dashboard, ถ้าเป็น User ให้ไปหน้าร้านค้า
                // if (user.Role == "Admin")
                // {
                //     return RedirectToAction("Index", "Admin");
                // }
                // else if (user.Role == "Warehouse")
                // {
                //     return RedirectToAction("Index", "Warehouse"); // เผื่ออนาคต
                // }
                
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "อีเมล หรือ รหัสผ่านไม่ถูกต้อง!";
            return View();
        }

        // ==========================================
        // 3. ฟังก์ชัน Logout
        // ==========================================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}