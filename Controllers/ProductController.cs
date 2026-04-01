using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;
using System.Collections.Generic;

namespace Stepify.Controllers
{
    public class ProductController : Controller
    {
        private readonly StepifyContext _db;

        public ProductController(StepifyContext db)
        {
            _db = db;
        }

        // ==========================================
        // หน้ารายละเอียดสินค้า (รับ id ของสินค้ามา)
        // ==========================================
        public IActionResult Details(int id)
        {
            // 1. ดึงข้อมูลสินค้าหลัก
            var product = _db.Products.FirstOrDefault(p => p.ProductId == id);
            
            // ถ้าหาสินค้าไม่เจอ (เผื่อคนพิมพ์ URL มั่ว) ให้เด้งกลับหน้าแรก
            if (product == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // 2. ดึงรูปภาพทั้งหมดของสินค้านี้ (เรียงให้รูป IsPrimary ขึ้นก่อน)
            var images = _db.ProductImages
                            .Where(i => i.ProductId == id)
                            .OrderByDescending(i => i.IsPrimary)
                            .ToList();

            // 3. ดึงตัวเลือกไซส์ (Variants) ที่ยังมีสต็อกเหลืออยู่
            var variants = _db.ProductVariants
                              .Where(v => v.ProductId == id && v.StockQty > 0)
                              .ToList();

            // ส่งรูปภาพและไซส์ไปพร้อมกับ ViewBag
            ViewBag.Images = images;
            ViewBag.Variants = variants;

            return View(product); // ส่งข้อมูลสินค้าหลักไปเป็น Model
        }
    }
}