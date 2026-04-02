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
    [Microsoft.AspNetCore.Authorization.Authorize] // ต้องล็อกอินถึงจะรีวิวได้
    public IActionResult AddReview(int ProductId, int Rating, string Comment)
    {
      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // 1. ค้นหา OrderId ล่าสุดที่ลูกค้าคนนี้เคยสั่งซื้อสินค้านี้ (เพราะตาราง Review บังคับเก็บ OrderId)
      var latestOrderId = (from o in _db.Orders
                           join od in _db.OrderDetails on o.OrderId equals od.OrderId
                           join v in _db.ProductVariants on od.VariantId equals v.VariantId
                           where o.UserId == currentUserId && v.ProductId == ProductId
                           orderby o.OrderDate descending
                           select o.OrderId).FirstOrDefault();

      // ถ้าหาบิลไม่เจอ แปลว่าไม่ได้ซื้อจริง ให้เด้งกลับ
      if (latestOrderId == 0) return RedirectToAction("Details", new { id = ProductId });

      // 2. เช็คว่าลูกค้าเคยรีวิวสินค้านี้ ในบิลนี้ไปแล้วหรือยัง? (ป้องกันการปั๊มรีวิวเอาคูปอง)
      bool alreadyReviewed = _db.Reviews.Any(r => r.UserId == currentUserId && r.ProductId == ProductId && r.OrderId == latestOrderId);

      if (!alreadyReviewed)
      {
        // 3. บันทึกข้อมูลรีวิวลงฐานข้อมูล
        var newReview = new Review
        {
          UserId = currentUserId,
          ProductId = ProductId,
          OrderId = latestOrderId,
          Rating = Rating,
          Comment = Comment,
          CreatedAt = DateTime.Now,
          IsRewardClaimed = true // บันทึกว่ารับรางวัลจากรีวิวนี้ไปแล้ว
        };
        _db.Reviews.Add(newReview);

        // 🌟 4. ไฮไลท์สำคัญ: แจกคูปอง 100 บาท เข้ากระเป๋าลูกค้าทันที!
        var rewardVoucher = new UserVoucher
        {
          UserId = currentUserId,
          VoucherType = "ส่วนลดแทนคำขอบคุณจากรีวิว",
          DiscountValue = 100,
          IsPercent = false, // ลดเป็นเงินสด 100 บาท
          IsUsed = false
        };
        _db.UserVouchers.Add(rewardVoucher);

        _db.SaveChanges(); // เซฟทุกอย่างลง Database

        TempData["SuccessMsg"] = "ขอบคุณสำหรับรีวิว! คุณได้รับคูปองส่วนลด 100 บาท สำหรับใช้งานในครั้งถัดไป";
      }
      else
      {
        TempData["ErrorMsg"] = "คุณได้รีวิวสินค้าจากคำสั่งซื้อนี้ไปแล้ว";
      }

      // รีเฟรชหน้าสินค้าเดิม
      return RedirectToAction("Details", new { id = ProductId });
    }

    public IActionResult AllProducts(string brand, string sort)
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

      // ระบบ Filter
      if (!string.IsNullOrEmpty(brand))
      {
        productsQuery = productsQuery.Where(p => p.Brand == brand);
      }

      // ระบบ Sort
      productsQuery = sort switch
      {
        "price_asc" => productsQuery.OrderBy(p => p.Price),
        "price_desc" => productsQuery.OrderByDescending(p => p.Price),
        _ => productsQuery.OrderByDescending(p => p.ProductId)
      };

      ViewBag.Brands = _db.Products.Select(p => p.Brand).Distinct().ToList();
      ViewBag.SelectedBrand = brand;
      ViewBag.SelectedSort = sort;

      return View(productsQuery.ToList());
    }
  }
}