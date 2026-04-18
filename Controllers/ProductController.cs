using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using Stepify.Models.ViewModels;
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
    // หน้ารายละเอียดสินค้า (พร้อมแสดงรีวิว)
    // ==========================================
    public IActionResult Details(int id)
    {
      var product = _db.Products.FirstOrDefault(p => p.ProductId == id);
      if (product == null) return NotFound();

      // ดึงข้อมูลรูปภาพและไซส์ (สมมติว่าคุณมีโค้ดดึงอยู่แล้ว)
      ViewBag.Images = _db.ProductImages.Where(i => i.ProductId == id).ToList();
      ViewBag.Variants = _db.ProductVariants.Where(v => v.ProductId == id).ToList();

      // 🌟 1. ดึงข้อมูลรีวิวทั้งหมดของสินค้านี้
      var reviews = (from r in _db.Reviews
                     where r.ProductId == id
                     join u in _db.Users on r.UserId equals u.UserId
                     orderby r.CreatedAt descending // 🛠️ เปลี่ยนจาก r.ReviewDate เป็น r.CreatedAt
                     select new
                     {
                       r.Rating,
                       r.Comment,
                       ReviewDate = r.CreatedAt, // 🛠️ ดึงค่า r.CreatedAt มาใส่ในชื่อ ReviewDate เพื่อให้หน้า View ยังแสดงผลได้เหมือนเดิม
                       Username = u.Username
                     }).ToList();

      ViewBag.Reviews = reviews;
      ViewBag.ReviewCount = reviews.Count;
      ViewBag.AverageRating = reviews.Any() ? reviews.Average(r => (double?)r.Rating ?? 0) : 0;

      // 🌟 2. เช็คว่าคนที่ล็อกอินอยู่ "เคยซื้อ" สินค้านี้ไหม?
      bool hasPurchased = false;
      bool alreadyReviewed = false; // เพิ่มตัวแปรเช็คการรีวิว

      if (User.Identity != null && User.Identity.IsAuthenticated)
      {
        int currentUserId = int.Parse(User.FindFirst("UserId").Value);

        // ดึง OrderId ล่าสุดที่ลูกค้าคนนี้ซื้อสินค้านี้
        var latestOrderId = (from o in _db.Orders
                             join od in _db.OrderDetails on o.OrderId equals od.OrderId
                             join v in _db.ProductVariants on od.VariantId equals v.VariantId
                             where o.UserId == currentUserId && v.ProductId == id
                             orderby o.OrderDate descending
                             select o.OrderId).FirstOrDefault();

        if (latestOrderId != 0)
        {
          hasPurchased = true;
          // เช็คว่าบิลล่าสุดนี้ เคยถูกนำมารีวิวสินค้านี้ไปแล้วหรือยัง
          alreadyReviewed = _db.Reviews.Any(r => r.UserId == currentUserId && r.ProductId == id && r.OrderId == latestOrderId);
        }
      }

      ViewBag.HasPurchased = hasPurchased;
      ViewBag.AlreadyReviewed = alreadyReviewed; // ส่งค่าไปบอกหน้าเว็บว่าเคยรีวิวแล้ว

      return View(product);
    }

    // ==========================================
    // ฟังก์ชันรับข้อมูลรีวิวจากลูกค้า (POST)
    // ==========================================
    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public IActionResult AddReview(ReviewViewModel model) // 🌟 2. เปลี่ยนให้รับค่าเป็นคลาส ReviewViewModel
    {
      // 🌟 3. ตรวจสอบว่าข้อมูลที่ส่งมาถูกต้องไหม (เช่น Rating เกิน 5 ไหม)
      if (!ModelState.IsValid)
      {
        TempData["ErrorMsg"] = "ข้อมูลรีวิวไม่ถูกต้อง";
        return RedirectToAction("Details", new { id = model.ProductId });
      }

      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // 1. ค้นหา OrderId ล่าสุด
      var latestOrderId = (from o in _db.Orders
                           join od in _db.OrderDetails on o.OrderId equals od.OrderId
                           join v in _db.ProductVariants on od.VariantId equals v.VariantId
                           where o.UserId == currentUserId && v.ProductId == model.ProductId // 🌟 4. แก้มาใช้ model.ProductId
                           orderby o.OrderDate descending
                           select o.OrderId).FirstOrDefault();

      if (latestOrderId == 0) return RedirectToAction("Details", new { id = model.ProductId });

      // 2. เช็คว่าลูกค้าเคยรีวิว
      bool alreadyReviewed = _db.Reviews.Any(r => r.UserId == currentUserId && r.ProductId == model.ProductId && r.OrderId == latestOrderId);

      if (!alreadyReviewed)
      {
        // 3. บันทึกข้อมูลรีวิวลงฐานข้อมูล
        var newReview = new Review
        {
          UserId = currentUserId,
          ProductId = model.ProductId, // 🌟 4. แก้มาใช้ model.ProductId
          OrderId = latestOrderId,
          Rating = model.Rating,       // 🌟 4. แก้มาใช้ model.Rating
          Comment = model.Comment,     // 🌟 4. แก้มาใช้ model.Comment
          CreatedAt = DateTime.Now,
          IsRewardClaimed = true
        };
        _db.Reviews.Add(newReview);

        // 4. แจกคูปอง
        var rewardVoucher = new UserVoucher
        {
          UserId = currentUserId,
          VoucherType = "ส่วนลดแทนคำขอบคุณจากรีวิว",
          DiscountValue = 100,
          IsPercent = false,
          IsUsed = false
        };
        _db.UserVouchers.Add(rewardVoucher);

        _db.SaveChanges();

        TempData["SuccessMsg"] = "ขอบคุณสำหรับรีวิว! คุณได้รับคูปองส่วนลด 100 บาท สำหรับใช้งานในครั้งถัดไป";
      }
      else
      {
        TempData["ErrorMsg"] = "คุณได้รีวิวสินค้าจากคำสั่งซื้อนี้ไปแล้ว";
      }

      return RedirectToAction("Details", new { id = model.ProductId });
    }

    // ==========================================
    // หน้ารวมสินค้าทั้งหมด (พร้อมระบบค้นหา, กรองแบรนด์, เรียงราคา)
    // ==========================================
    public IActionResult AllProducts(string searchKeyword, string brand, string sort)
    {
      // ดึงสินค้าพร้อมรูปหลัก เหมือนหน้า Home
      var productsQuery = (from p in _db.Products
                           where p.IsActive == true
                           let primaryImg = _db.ProductImages.FirstOrDefault(i => i.ProductId == p.ProductId && i.IsPrimary == true)
                           select new
                           {
                             p.ProductId,
                             p.Name,
                             p.Brand,
                             p.Price,
                             ImageUrl = primaryImg != null ? primaryImg.ImageUrl : "https://via.placeholder.com/300x300",
                             p.Description
                           }).AsQueryable();

      // 🌟 1. ระบบ Search (ค้นหาด้วยการพิมพ์ชื่อ หรือ แบรนด์)
      if (!string.IsNullOrEmpty(searchKeyword))
      {
        productsQuery = productsQuery.Where(p => p.Name.Contains(searchKeyword) || p.Brand.Contains(searchKeyword));
      }

      // 🌟 2. ระบบ Filter (กรองด้วยแบรนด์)
      if (!string.IsNullOrEmpty(brand))
      {
        productsQuery = productsQuery.Where(p => p.Brand == brand);
      }

      // 🌟 3. ระบบ Sort (เรียงลำดับราคา)
      productsQuery = sort switch
      {
        "price_asc" => productsQuery.OrderBy(p => p.Price),
        "price_desc" => productsQuery.OrderByDescending(p => p.Price),
        _ => productsQuery.OrderByDescending(p => p.ProductId)
      };

      // ส่งข้อมูลไปวาดตัวเลือกที่หน้า View
      ViewBag.Brands = _db.Products.Select(p => p.Brand).Distinct().ToList();
      ViewBag.SelectedBrand = brand;
      ViewBag.SelectedSort = sort;
      ViewBag.SearchKeyword = searchKeyword; // ส่งคำที่ค้นหากลับไปแสดงโชว์ในช่องค้นหา

      return View(productsQuery.ToList());
    }
  }
}