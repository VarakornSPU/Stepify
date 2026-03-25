using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;

namespace Stepify.Controllers
{
    public class HomeController : Controller
    {
        private readonly StepifyContext _db;

        public HomeController(StepifyContext db)
        {
            _db = db;
        }

        // ==========================================
        // 1. หน้าแรกของเว็บ (แสดงสินค้าทั้งหมดที่ IsActive = true)
        // ==========================================
        public IActionResult Index()
        {
            // ดึงสินค้าเฉพาะที่เปิดขายอยู่ (IsActive == true)
            var products = (from p in _db.Products
                            where p.IsActive == true
                            // หารูปหน้าปก
                            let img = _db.ProductImages.FirstOrDefault(i => i.ProductId == p.ProductId && i.IsPrimary == true)
                            select new
                            {
                                p.ProductId,
                                p.Name,
                                p.Brand,
                                p.Price,
                                // ถ้ารูปไม่มี ให้ใช้รูป placeholder แบลงค์ๆ
                                ImageUrl = img != null ? img.ImageUrl : "https://via.placeholder.com/300x300?text=No+Image"
                            }).ToList();

            ViewBag.Products = products;
            return View();
        }

        // ==========================================
        // 2. ฟังก์ชันค้นหาสินค้า (FC3)
        // ==========================================
        public IActionResult Search(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return RedirectToAction("Index");
            }

            var products = (from p in _db.Products
                            where p.IsActive == true && (p.Name.Contains(keyword) || p.Brand.Contains(keyword))
                            let img = _db.ProductImages.FirstOrDefault(i => i.ProductId == p.ProductId && i.IsPrimary == true)
                            select new
                            {
                                p.ProductId,
                                p.Name,
                                p.Brand,
                                p.Price,
                                ImageUrl = img != null ? img.ImageUrl : "https://via.placeholder.com/300x300?text=No+Image"
                            }).ToList();

            ViewBag.Products = products;
            ViewBag.Keyword = keyword; // ส่งคำค้นหากลับไปโชว์ที่หน้าเว็บด้วย
            
            return View("Index"); // ใช้หน้า Index.cshtml ในการแสดงผลการค้นหาได้เลย
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}