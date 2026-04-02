using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

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

            // ===================================================
            // 🌟 แจกคูปอง "สมาชิกใหม่" ทันที 10% (ใช้ได้ 1 ครั้ง)
            // ===================================================
            // 🌟 แจกคูปองสมาชิกใหม่ 10% ให้ทันที!
            var welcomeVoucher = new UserVoucher
            {
                UserId = newUser.UserId,
                VoucherType = "คูปองต้อนรับสมาชิกใหม่ (ลด 10%)",
                DiscountValue = 10,
                IsPercent = true,
                IsUsed = false
            };
            _db.UserVouchers.Add(welcomeVoucher);
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

        // ==========================================
        // หน้าโปรไฟล์ส่วนตัว (GET)
        // ==========================================
        [Authorize]
        public IActionResult Profile()
        {
            // ดึง ID ของคนที่ล็อกอินอยู่
            int currentUserId = int.Parse(User.FindFirst("UserId").Value);

            // ดึงข้อมูลลูกค้าคนนั้นจากฐานข้อมูลส่งไปที่หน้าจอ
            var user = _db.Users.FirstOrDefault(u => u.UserId == currentUserId);
            return View(user);
        }

        // ==========================================
        // 5. ฟังก์ชันอัปเดตโปรไฟล์ (POST)
        // ==========================================
        [HttpPost]
        [Authorize]
        // 🌟 1. เปลี่ยนเป็น async Task<IActionResult> 
        public async Task<IActionResult> UpdateProfile(string Username, string Tel, string Address, string NewPassword)
        {
            int currentUserId = int.Parse(User.FindFirst("UserId").Value);
            var user = _db.Users.FirstOrDefault(u => u.UserId == currentUserId);

            if (user != null)
            {
                // อัปเดตข้อมูลลง Database
                user.Username = Username;
                user.Tel = Tel;
                user.Address = Address;

                if (!string.IsNullOrEmpty(NewPassword))
                {
                    user.Password = NewPassword;
                }

                _db.Users.Update(user);
                _db.SaveChanges();

                // ========================================================
                // 🌟 2. สิ่งที่เพิ่มเข้ามา: อัปเดต Cookie ล็อกอินใหม่ทันที!
                // ========================================================
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username ?? ""), // อัปเดตชื่อใหม่ใส่ Cookie
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Role, user.Role ?? "User"),
                    new Claim("UserId", user.UserId.ToString())
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                // สั่ง Sign in ทับของเดิม (เสมือนล็อกอินใหม่เงียบๆ หลังบ้าน)
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            }

            TempData["SuccessMsg"] = "อัปเดตข้อมูลโปรไฟล์เรียบร้อยแล้ว";
            return RedirectToAction("Profile");
        }
    }
}