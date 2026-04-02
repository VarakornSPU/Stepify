using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using Microsoft.AspNetCore.Hosting; // เพิ่มบรรทัดนี้สำหรับการอัปโหลดไฟล์
using Microsoft.AspNetCore.Http; // เพิ่มบรรทัดนี้สำหรับ IFormFile
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // เพิ่มบรรทัดนี้

namespace Stepify.Controllers
{
    // บังคับว่าต้อง Login และต้องมี Role เป็น "Admin" เท่านั้น ถึงจะเข้าหน้านี้ได้!
    [Authorize(Roles = "Admin, Warehouse")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
        public IActionResult ManageUsers()
        {
            // ดึงข้อมูล User ทั้งหมดมาแสดง (สไตล์ LINQ ที่คุณถนัด)
            var users = (from u in _db.Users select u).ToList();
            return View(users);
        }

        // ==========================================
        // 2. ฟังก์ชันเพิ่มผู้ใช้ (Add) - รับค่าจาก Modal
        // ==========================================
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
        public IActionResult EditUser(int id) // รับ Parameter ชื่อ id (ไม่ต้องแปลง string แล้ว)
        {
            var check = (from us in _db.Users where us.UserId == id select us).FirstOrDefault();
            return View(check);
        }

        // ==========================================
        // 4. ฟังก์ชันบันทึกข้อมูลที่แก้ไข (Edit - POST)
        // ==========================================
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        // 7. ฟังก์ชันเพิ่มสินค้า รูปภาพ และ ไซส์แบบหลายรายการ
        // ==========================================
        [HttpPost]
        // สังเกตว่า Sizes, Colors, StockQtys ถูกเปลี่ยนเป็น List เพื่อรับค่าได้หลายอันพร้อมกัน
        public async Task<IActionResult> AddProduct(Product data, List<IFormFile> ImageFiles, List<string> Sizes, List<string> Colors, List<int> StockQtys)
        {
            // 1. บันทึกข้อมูลสินค้าหลัก
            var newProduct = new Product
            {
                Name = data.Name,
                Brand = data.Brand,
                Price = data.Price,
                Description = data.Description,
                IsActive = true
            };
            _db.Products.Add(newProduct);
            _db.SaveChanges();

            // ========================================================
            // 🆕 2. วนลูปเซฟไซส์รองเท้าทั้งหมดที่แอดมินกรอกเข้ามา
            // ========================================================
            if (Sizes != null && Sizes.Count > 0)
            {
                for (int i = 0; i < Sizes.Count; i++)
                {
                    // เช็คกันเหนียวว่าช่องไซส์ต้องไม่ว่าง และสต๊อกต้องมากกว่า 0
                    if (!string.IsNullOrWhiteSpace(Sizes[i]) && StockQtys[i] > 0)
                    {
                        var newVariant = new ProductVariant
                        {
                            ProductId = newProduct.ProductId,
                            Size = Sizes[i],
                            // ป้องกันกรณีแอดมินลืมกรอกสี
                            Color = (Colors != null && Colors.Count > i) ? Colors[i] : "-",
                            StockQty = StockQtys[i]
                        };
                        _db.ProductVariants.Add(newVariant);
                    }
                }
                _db.SaveChanges(); // เซฟไซส์ทั้งหมดลงตาราง
            }

            // 3. จัดการรูปภาพหลายรูป (โค้ดเดิม)
            if (ImageFiles != null && ImageFiles.Count > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "products");
                if (!Directory.Exists(uploadsFolder)) { Directory.CreateDirectory(uploadsFolder); }

                bool isFirstImage = true;
                foreach (var file in ImageFiles)
                {
                    if (file.Length > 0)
                    {
                        string uniqueFileName = System.Guid.NewGuid().ToString() + "_" + file.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        var newImage = new ProductImage
                        {
                            ProductId = newProduct.ProductId,
                            ImageUrl = "/images/products/" + uniqueFileName,
                            IsPrimary = isFirstImage
                        };
                        _db.ProductImages.Add(newImage);
                        isFirstImage = false;
                    }
                }
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
                // 1. ลบข้อมูลรูปภาพที่ผูกอยู่
                var images = _db.ProductImages.Where(i => i.ProductId == id).ToList();
                _db.ProductImages.RemoveRange(images);

                // 🌟 2. (เพิ่มใหม่) ลบข้อมูลไซส์และสต๊อกที่ผูกอยู่
                var variants = _db.ProductVariants.Where(v => v.ProductId == id).ToList();
                _db.ProductVariants.RemoveRange(variants);

                // 3. ค่อยลบสินค้าหลัก
                _db.Products.Remove(product);
                _db.SaveChanges();
            }
            return RedirectToAction("ManageProducts");
        }
        // ==========================================
        // 9. ฟังก์ชันดึงข้อมูลสินค้ามาแสดงเพื่อแก้ไข (GET)
        // ==========================================
        public IActionResult EditProduct(int id)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == id);
            if (product == null) return RedirectToAction("ManageProducts");

            // ดึงข้อมูลรูปภาพและไซส์ของสินค้านี้แนบไปด้วย
            ViewBag.Images = _db.ProductImages.Where(i => i.ProductId == id).ToList();
            ViewBag.Variants = _db.ProductVariants.Where(v => v.ProductId == id).ToList();

            return View(product);
        }

        // ==========================================
        // 10. ฟังก์ชันบันทึกข้อมูลสินค้าที่แก้ไขแล้ว (POST)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> EditProduct(Product data, int[] VariantIds, string[] Sizes, string[] Colors, int[] StockQtys, List<IFormFile> ImageFiles)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == data.ProductId);
            if (product == null) return RedirectToAction("ManageProducts");

            // 1. อัปเดตข้อมูลหลัก
            product.Name = data.Name;
            product.Brand = data.Brand;
            product.Price = data.Price;
            product.Description = data.Description;
            _db.Products.Update(product);

            // 2. อัปเดตไซส์และสต๊อก
            // เราใช้ Array (VariantIds, Sizes, Colors) รับค่ามาจากหน้าเว็บแบบเรียงตามลำดับ
            for (int i = 0; i < Sizes.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(Sizes[i]))
                {
                    // ถ้ามี VariantId ส่งมา แปลว่าเป็นของเดิม ให้อัปเดต
                    if (i < VariantIds.Length && VariantIds[i] > 0)
                    {
                        var v = _db.ProductVariants.FirstOrDefault(x => x.VariantId == VariantIds[i]);
                        if (v != null)
                        {
                            v.Size = Sizes[i];
                            v.Color = Colors[i] ?? "-";
                            v.StockQty = StockQtys[i];
                            _db.ProductVariants.Update(v);
                        }
                    }
                    else // ถ้า VariantId เป็น 0 หรือไม่มี แปลว่าแอดมินกด "+ เพิ่มไซส์อื่น" เข้ามาใหม่
                    {
                        var newVariant = new ProductVariant
                        {
                            ProductId = product.ProductId,
                            Size = Sizes[i],
                            Color = Colors[i] ?? "-",
                            StockQty = StockQtys[i]
                        };
                        _db.ProductVariants.Add(newVariant);
                    }
                }
            }

            // 3. เพิ่มรูปภาพใหม่ (ถ้ามีการอัปโหลดมาเพิ่ม)
            if (ImageFiles != null && ImageFiles.Count > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "products");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                // เช็คว่ามีรูปหน้าปกอยู่แล้วหรือยัง
                bool hasPrimary = _db.ProductImages.Any(i => i.ProductId == product.ProductId && i.IsPrimary == true);

                foreach (var file in ImageFiles)
                {
                    if (file.Length > 0)
                    {
                        string uniqueFileName = System.Guid.NewGuid().ToString() + "_" + file.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        var newImg = new ProductImage
                        {
                            ProductId = product.ProductId,
                            ImageUrl = "/images/products/" + uniqueFileName,
                            IsPrimary = !hasPrimary // ถ้ารูปแรกยังไม่มี ให้รูปที่อัปใหม่เป็นหน้าปก
                        };
                        _db.ProductImages.Add(newImg);
                        hasPrimary = true;
                    }
                }
            }

            _db.SaveChanges();
            return RedirectToAction("ManageProducts");
        }
        // ==========================================
        // 11. หน้าจัดการคำสั่งซื้อทั้งหมด (Manage Orders)
        // ==========================================
        public IActionResult ManageOrders()
        {
            // ดึงออเดอร์ทั้งหมดในระบบ เรียงจากบิลล่าสุดไปเก่าสุด
            var orders = _db.Orders.OrderByDescending(o => o.OrderDate).ToList();
            return View(orders);
        }

        // ==========================================
        // 12. หน้ารายละเอียดคำสั่งซื้อสำหรับ Admin (GET)
        // ==========================================
        public IActionResult OrderDetails(int id)
        {
            var order = _db.Orders.FirstOrDefault(o => o.OrderId == id);
            if (order == null) return RedirectToAction("ManageOrders");

            // ดึงข้อมูลสินค้าในบิล
            var orderDetails = (from od in _db.OrderDetails
                                where od.OrderId == id
                                join v in _db.ProductVariants on od.VariantId equals v.VariantId
                                join p in _db.Products on v.ProductId equals p.ProductId
                                let img = _db.ProductImages.FirstOrDefault(i => i.ProductId == p.ProductId && i.IsPrimary == true)
                                select new
                                {
                                    p.Name,
                                    v.Size,
                                    v.Color,
                                    od.UnitPrice,
                                    od.Quantity,
                                    od.SubTotal,
                                    ImageUrl = img != null ? img.ImageUrl : "https://via.placeholder.com/100"
                                }).ToList();

            ViewBag.OrderDetails = orderDetails;

            // ดึงชื่อลูกค้าคนสั่งมาโชว์ให้แอดมินดูด้วย
            ViewBag.Customer = _db.Users.FirstOrDefault(u => u.UserId == order.UserId);

            return View(order);
        }

        // ==========================================
        // 13. ฟังก์ชันอัปเดตสถานะออเดอร์ (POST)
        // ==========================================
        [HttpPost]
        public IActionResult UpdateOrderStatus(int OrderId, string PaymentStatus, string ShippingStatus, string TrackingNumber)
        {
            var order = _db.Orders.FirstOrDefault(o => o.OrderId == OrderId);
            if (order != null)
            {
                order.PaymentStatus = PaymentStatus;
                order.ShippingStatus = ShippingStatus;
                order.TrackingNumber = TrackingNumber;

                _db.Orders.Update(order);
                _db.SaveChanges();
            }
            // เซฟเสร็จแล้วให้เด้งกลับมาหน้าเดิม
            return RedirectToAction("OrderDetails", new { id = OrderId });
        }

        // ==========================================
        // 16. หน้ารายงานยอดขาย (Sales Report)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public IActionResult SalesReport()
        {
            // 1. ดึงเฉพาะบิลที่ "ชำระเงินแล้ว (Paid)" เท่านั้น
            var paidOrders = _db.Orders.Where(o => o.PaymentStatus == "Paid").ToList();

            // สรุปยอดรวมทั้งหมด
            ViewBag.TotalSales = paidOrders.Sum(o => o.NetAmount);
            ViewBag.TotalOrders = paidOrders.Count;

            // 2. เตรียมข้อมูลสำหรับทำกราฟแท่ง (ยอดขาย 12 เดือน ในปีปัจจุบัน)
            int currentYear = DateTime.Now.Year;
            decimal[] monthlySalesData = new decimal[12];

            var salesByMonth = paidOrders
                .Where(o => o.OrderDate.HasValue && o.OrderDate.Value.Year == currentYear)
                .GroupBy(o => o.OrderDate.Value.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(x => x.NetAmount) })
                .ToList();

            // เอาข้อมูลยอดขายไปหยอดลงใน Array 12 เดือน (ม.ค. - ธ.ค.)
            foreach (var item in salesByMonth)
            {
                monthlySalesData[item.Month - 1] = item.Total;
            }
            // แปลงเป็น String คั่นด้วยลูกน้ำ เพื่อส่งไปให้ JavaScript วาดกราฟ
            ViewBag.ChartData = string.Join(",", monthlySalesData);
            ViewBag.CurrentYear = currentYear;

            // 3. จัดอันดับสินค้าขายดี 5 อันดับแรก (Top 5 Best Sellers)
            var topProducts = (from od in _db.OrderDetails
                               join o in _db.Orders on od.OrderId equals o.OrderId
                               where o.PaymentStatus == "Paid"
                               join v in _db.ProductVariants on od.VariantId equals v.VariantId
                               join p in _db.Products on v.ProductId equals p.ProductId
                               group od by new { p.ProductId, p.Name } into g
                               orderby g.Sum(x => x.Quantity) descending
                               select new
                               {
                                   ProductName = g.Key.Name,
                                   TotalSold = g.Sum(x => x.Quantity),
                                   TotalRevenue = g.Sum(x => x.SubTotal)
                               }).Take(5).ToList();

            ViewBag.TopProducts = topProducts;

            return View();
        }

        // ==========================================
        // 17. หน้าจัดการโปรโมชั่น (Manage Promotions)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public IActionResult ManagePromotions()
        {
            // ดึงแบรนด์ทั้งหมดที่มีในระบบ (ตัดค่าซ้ำ และเอาเฉพาะที่ไม่ว่างเปล่า)
            ViewBag.Brands = _db.Products
                                .Where(p => !string.IsNullOrEmpty(p.Brand))
                                .Select(p => p.Brand)
                                .Distinct()
                                .ToList();

            var promos = _db.Promotions.OrderByDescending(p => p.PromotionId).ToList();
            return View(promos);
        }

       // ==========================================
        // 18. ฟังก์ชันเพิ่มโปรโมชั่นใหม่ (POST)
        // ==========================================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddPromotion(Promotion data)
        {
            // 🌟 พระเอกกู้ชีพ: แก้ปัญหา พ.ศ. กับ ค.ศ. ตีกัน
            if (data.ExpiryDate.HasValue && data.ExpiryDate.Value.Year < 1753)
            {
                if (data.ExpiryDate.Value.Year == 1)
                {
                    // กรณีบั๊กปี 0001 ให้เป็นค่าว่าง (ไม่มีวันหมดอายุ)
                    data.ExpiryDate = null;
                }
                else
                {
                    // กรณีที่ C# เข้าใจผิดว่า ค.ศ. 2026 คือ พ.ศ. 2026 (ลบไปจนเหลือ ค.ศ. 1483)
                    // เราก็แค่บวก 543 คืนกลับไป ให้มันกลายเป็น 2026 เหมือนเดิม!
                    data.ExpiryDate = data.ExpiryDate.Value.AddYears(543);
                }
            }

            // บังคับให้โปรโมชั่นที่สร้างใหม่ เปิดใช้งานทันที
            data.IsActive = true; 
            
            _db.Promotions.Add(data);
            _db.SaveChanges();

            return RedirectToAction("ManagePromotions");
        }

        // ==========================================
        // 19. ฟังก์ชันเปิด/ปิด การใช้งานโค้ดส่วนลด
        // ==========================================
        [Authorize(Roles = "Admin")]
        public IActionResult TogglePromotion(int id)
        {
            var promo = _db.Promotions.FirstOrDefault(p => p.PromotionId == id);
            if (promo != null)
            {
                // สลับสถานะ (ถ้าเป็น true จะกลายเป็น false, ถ้า false จะกลายเป็น true)
                promo.IsActive = !promo.IsActive;
                _db.Promotions.Update(promo);
                _db.SaveChanges();
            }
            return RedirectToAction("ManagePromotions");
        }

        // ==========================================
        // 20. ฟังก์ชันลบโค้ดส่วนลด (Delete)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public IActionResult DeletePromotion(int id)
        {
            var promo = _db.Promotions.FirstOrDefault(p => p.PromotionId == id);
            if (promo != null)
            {
                _db.Promotions.Remove(promo);
                _db.SaveChanges();
            }
            return RedirectToAction("ManagePromotions");
        }
    }
}