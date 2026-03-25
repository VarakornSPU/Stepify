using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using Microsoft.AspNetCore.Hosting; // เพิ่มบรรทัดนี้สำหรับการอัปโหลดไฟล์
using Microsoft.AspNetCore.Http; // เพิ่มบรรทัดนี้สำหรับ IFormFile
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Stepify.Controllers
{
    public class AdminController : Controller
    {
        private readonly StepifyContext _db;
        private readonly IWebHostEnvironment _env; // ตัวแปรสำหรับจัดการ Path โฟลเดอร์

        // อัปเดต Constructor ให้รับ IWebHostEnvironment มาด้วย
        public AdminController(StepifyContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public IActionResult Index()
        {
            // ดึงสถิติภาพรวมมาโชว์ใน Dashboard (ใช้ LINQ ดึงจำนวน)
            ViewBag.TotalUsers = _db.Users.Count();
            ViewBag.TotalProducts = _db.Products.Count();
            ViewBag.TotalOrders = _db.Orders.Count(); // ถ้ายังไม่มีตารางนี้ คอมเมนต์ไว้ก่อนได้ครับ
            
            return View();
        }

        // ==========================================
        // 1. หน้าแสดงรายชื่อผู้ใช้ทั้งหมด (List)
        // ==========================================
        public IActionResult ManageUsers()
        {
            // ดึงข้อมูล User ทั้งหมดมาแสดง (สไตล์ LINQ ที่คุณถนัด)
            var users = (from u in _db.Users select u).ToList();
            return View(users);
        }

        // ==========================================
        // 2. ฟังก์ชันเพิ่มผู้ใช้ (Add) - รับค่าจาก Modal
        // ==========================================
        [HttpPost]
        public IActionResult AddUser(User data)
        {
            // ไม่ต้องรับ UserId เพราะ DB ตั้งเป็น IDENTITY (Auto Increment) ไว้แล้ว
            var newUser = new User
            {
                Username = data.Username,
                Email = data.Email,
                Tel = data.Tel,
                Password = data.Password, 
                Role = data.Role ?? "User" // ถ้าไม่ได้เลือก ให้เป็น User ทั่วไป
            };

            _db.Users.Add(newUser);
            _db.SaveChanges();
            
            return RedirectToAction("ManageUsers"); // กลับไปหน้า List
        }

        // ==========================================
        // 3. ฟังก์ชันดึงข้อมูลไปแสดงหน้าแก้ไข (Edit - GET)
        // ==========================================
        public IActionResult EditUser(int id) // รับ Parameter ชื่อ id (ไม่ต้องแปลง string แล้ว)
        {
            var check = (from us in _db.Users where us.UserId == id select us).FirstOrDefault();
            return View(check);
        }

        // ==========================================
        // 4. ฟังก์ชันบันทึกข้อมูลที่แก้ไข (Edit - POST)
        // ==========================================
        [HttpPost]
        public IActionResult EditUser(User data)
        {
            var user = (from u in _db.Users where u.UserId == data.UserId select u).FirstOrDefault();
            
            if (user != null)
            {
                user.Username = data.Username;
                user.Email = data.Email;
                user.Tel = data.Tel;
                user.Password = data.Password;
                user.Role = data.Role; // อัปเดต Role ได้ด้วย

                _db.Update(user);
                _db.SaveChanges();
            }
            return RedirectToAction("ManageUsers");
        }

        // ==========================================
        // 5. ฟังก์ชันลบผู้ใช้ (Delete)
        // ==========================================
        public IActionResult DeleteUser(int id)
        {
            var user = (from u in _db.Users where u.UserId == id select u).FirstOrDefault();
            if (user != null)
            {
                _db.Users.Remove(user);
                _db.SaveChanges();
            }
            return RedirectToAction("ManageUsers");
        }
        // ==========================================
        // 6. หน้าแสดงรายการสินค้า (Product List)
        // ==========================================
        public IActionResult ManageProducts()
        {
            // ดึงข้อมูลสินค้า และดึงรูปภาพหน้าปก (IsPrimary == true) มาโชว์ด้วย
            // สังเกตว่าเราใช้ LINQ ในการผูกข้อมูล (Join แบบไม่ง้อ FK)
            var products = (from p in _db.Products
                            let img = _db.ProductImages.FirstOrDefault(i => i.ProductId == p.ProductId && i.IsPrimary == true)
                            select new
                            {
                                p.ProductId,
                                p.Name,
                                p.Brand,
                                p.Price,
                                p.IsActive,
                                ImageUrl = img != null ? img.ImageUrl : "/images/no-image.png" // ถ้ารูปไม่มีให้ใช้รูปพื้นฐาน
                            }).ToList();

            ViewBag.Products = products; // ส่งข้อมูลผ่าน ViewBag ไปที่หน้า HTML
            return View();
        }

        // ==========================================
        // 7. ฟังก์ชันเพิ่มสินค้า และอัปโหลดรูป (Add Product)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> AddProduct(Product data, IFormFile ImageFile)
        {
            // 1. บันทึกข้อมูลสินค้าหลักลงตาราง Products ก่อน
            var newProduct = new Product
            {
                Name = data.Name,
                Brand = data.Brand,
                Price = data.Price,
                Description = data.Description,
                IsActive = true
            };
            _db.Products.Add(newProduct);
            _db.SaveChanges(); // เซฟปุ๊บ ระบบจะสร้าง ProductId ให้อัตโนมัติ

            // 2. จัดการรูปภาพ (ถ้ามีการอัปโหลดไฟล์เข้ามา)
            if (ImageFile != null && ImageFile.Length > 0)
            {
                // ระบุตำแหน่งที่จะเซฟไฟล์ (wwwroot/images/products)
                string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "products");
                
                // ถ้าโฟลเดอร์ยังไม่มี ให้สร้างใหม่
                if (!Directory.Exists(uploadsFolder)) { Directory.CreateDirectory(uploadsFolder); }

                // ตั้งชื่อไฟล์ใหม่ด้วยสุ่มตัวอักษร (Guid) ป้องกันชื่อไฟล์ซ้ำกัน
                string uniqueFileName = System.Guid.NewGuid().ToString() + "_" + ImageFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // เอาไฟล์ไปวางเซฟในโฟลเดอร์
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(fileStream);
                }

                // 3. เอา Path ไปบันทึกลงตาราง ProductImages
                var newImage = new ProductImage
                {
                    ProductId = newProduct.ProductId, // ผูกกับสินค้าที่เพิ่งสร้าง
                    ImageUrl = "/images/products/" + uniqueFileName,
                    IsPrimary = true // ตั้งให้เป็นรูปหน้าปก
                };
                _db.ProductImages.Add(newImage);
                _db.SaveChanges();
            }

            return RedirectToAction("ManageProducts");
        }

        // ==========================================
        // 8. ฟังก์ชันลบสินค้า (Delete)
        // ==========================================
        public IActionResult DeleteProduct(int id)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == id);
            if (product != null)
            {
                // ควรลบข้อมูลรูปที่ผูกอยู่ก่อน (เพราะเราไม่มี FK คอยลบให้อัตโนมัติ)
                var images = _db.ProductImages.Where(i => i.ProductId == id).ToList();
                _db.ProductImages.RemoveRange(images);
                
                // ค่อยลบสินค้าหลัก
                _db.Products.Remove(product);
                _db.SaveChanges();
            }
            return RedirectToAction("ManageProducts");
        }
    }
}