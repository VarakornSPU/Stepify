using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using System;

namespace Stepify.Controllers
{
  [Authorize] // ต้องล็อกอินก่อนสั่งซื้อเสมอ
  public class OrderController : Controller
  {
    private readonly StepifyContext _db;

    public OrderController(StepifyContext db)
    {
      _db = db;
    }

    // ==========================================
    // 1. หน้า Checkout สรุปยอดและกรอกที่อยู่
    // ==========================================
    public IActionResult Checkout(string promoCode = "")
    {
      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // ดึงข้อมูลตะกร้า
      var cartItems = (from c in _db.ShoppingCarts
                       where c.UserId == currentUserId
                       join v in _db.ProductVariants on c.VariantId equals v.VariantId
                       join p in _db.Products on v.ProductId equals p.ProductId
                       select new
                       {
                         c.VariantId,
                         p.Name,
                         v.Size,
                         v.Color,
                         p.Price,
                         Quantity = c.Quantity ?? 1, // จัดการกรณี Quantity เป็น null
                         SubTotal = p.Price * (c.Quantity ?? 1)
                       }).ToList();

      if (!cartItems.Any())
      {
        return RedirectToAction("Index", "Cart");
      }

      // คำนวณยอดเงิน
      decimal totalAmount = cartItems.Sum(x => x.SubTotal);
      decimal discountAmount = 0;

      // 🌟 แก้ไข: ตรวจสอบโค้ดส่วนลดให้ตรงกับตาราง Promotion ของคุณ
      if (!string.IsNullOrEmpty(promoCode))
      {
        // สมมติว่าใช้ ConditionValue เก็บตัวอักษรโค้ดส่วนลด
        var promo = _db.Promotions.FirstOrDefault(p => p.ConditionValue == promoCode && p.IsActive == true);

        if (promo != null)
        {
          // เช็คว่าเป็นลดแบบ % หรือลดเป็นบาท
          if (promo.IsPercent == true)
          {
            discountAmount = totalAmount * ((promo.DiscountValue ?? 0) / 100m);
            ViewBag.PromoMessage = $"ใช้โค้ดสำเร็จ! ลด {promo.DiscountValue}%";
          }
          else
          {
            discountAmount = promo.DiscountValue ?? 0;
            ViewBag.PromoMessage = $"ใช้โค้ดสำเร็จ! ลด {promo.DiscountValue} บาท";
          }
        }
        else
        {
          ViewBag.PromoError = "โค้ดส่วนลดไม่ถูกต้อง หรือหมดอายุแล้ว";
        }
      }

      ViewBag.CartItems = cartItems;
      ViewBag.TotalAmount = totalAmount;
      ViewBag.DiscountAmount = discountAmount;
      ViewBag.NetAmount = totalAmount - discountAmount;
      ViewBag.PromoCode = promoCode;

      return View();
    }

    // ==========================================
    // 2. ฟังก์ชันยืนยันคำสั่งซื้อ (Place Order)
    // ==========================================
    [HttpPost]
    public IActionResult PlaceOrder(string ShippingAddress, string PaymentMethod, decimal TotalAmount, decimal DiscountAmount, decimal NetAmount)
    {
      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // 🌟 1. เพิ่ม Logic เช็คสถานะการจ่ายเงิน
      // ถ้าเลือกเก็บเงินปลายทาง (COD) ให้เป็น Pending, ถ้าเป็นวิธีอื่น (บัตร/โอน) ให้เป็น Paid ทันที
      string status = (PaymentMethod == "COD") ? "Pending" : "Paid";

      // 🌟 2. สร้างบิลคำสั่งซื้อหลัก
      var newOrder = new Order
      {
        UserId = currentUserId,
        OrderDate = DateTime.Now,
        TotalAmount = TotalAmount,
        PromoDiscount = DiscountAmount,
        NetAmount = NetAmount,
        PaymentStatus = status,     // <--- เปลี่ยนมารับค่าจากตัวแปร status ตรงนี้ครับ
        ShippingStatus = "Packing",
        ShippingAddress = ShippingAddress
      };

      _db.Orders.Add(newOrder);
      _db.SaveChanges(); // เซฟเพื่อให้ได้ OrderId ออกมาก่อน

      // 3. ดึงสินค้าในตะกร้ามาทำ OrderDetails และ ตัดสต๊อก
      var cartItems = _db.ShoppingCarts.Where(c => c.UserId == currentUserId).ToList();

      foreach (var item in cartItems)
      {
        // ดึงไซส์สินค้า
        var variant = _db.ProductVariants.FirstOrDefault(v => v.VariantId == item.VariantId);

        if (variant != null)
        {
          var product = _db.Products.FirstOrDefault(p => p.ProductId == variant.ProductId);

          if (product != null)
          {
            // ดึงจำนวนออกมา และแปลง int? ให้เป็น int ปกติ
            int buyQuantity = item.Quantity ?? 1;

            // บันทึกรายการย่อย
            var orderDetail = new OrderDetail
            {
              OrderId = newOrder.OrderId,
              VariantId = item.VariantId,
              Quantity = buyQuantity,
              UnitPrice = product.Price,
              SubTotal = product.Price * buyQuantity
            };
            _db.OrderDetails.Add(orderDetail);

            // ตัดสต๊อกสินค้าอย่างปลอดภัย
            variant.StockQty = (variant.StockQty ?? 0) - buyQuantity;
            _db.ProductVariants.Update(variant);
          }
        }
      }

      // 4. ล้างตะกร้าสินค้า
      _db.ShoppingCarts.RemoveRange(cartItems);

      // 5. เซฟทุกอย่างลงฐานข้อมูล
      _db.SaveChanges();

      return RedirectToAction("Success", new { orderId = newOrder.OrderId });
    }

    // ==========================================
    // 3. หน้าขอบคุณหลังจากสั่งซื้อเสร็จ
    // ==========================================
    public IActionResult Success(int orderId)
    {
      ViewBag.OrderId = orderId;
      return View();
    }

    // ==========================================
    // 4. หน้าประวัติการสั่งซื้อ (My Orders)
    // ==========================================
    public IActionResult History()
    {
      // ดึง ID ของคนที่ล็อกอินอยู่
      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // ดึงรายการคำสั่งซื้อทั้งหมดของคนๆ นี้ เรียงจากใหม่ไปเก่า (OrderByDescending)
      var orders = _db.Orders
                      .Where(o => o.UserId == currentUserId)
                      .OrderByDescending(o => o.OrderDate)
                      .ToList();

      return View(orders);
    }

    // ==========================================
    // 5. หน้ารายละเอียดของคำสั่งซื้อ (Order Details)
    // ==========================================
    public IActionResult Details(int id)
    {
      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // ดึงข้อมูลบิลหลัก (ต้องเช็ค UserId ด้วย ป้องกันคนอื่นสวมรอยพิมพ์ ID มั่วๆ)
      var order = _db.Orders.FirstOrDefault(o => o.OrderId == id && o.UserId == currentUserId);

      if (order == null)
      {
        return RedirectToAction("History"); // ถ้าไม่เจอบิล ให้เด้งกลับหน้าประวัติ
      }

      // ดึงข้อมูลสินค้าย่อยในบิลนั้น มา Join เพื่อเอาชื่อ ไซส์ และรูปภาพมาโชว์
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

      return View(order); // ส่งข้อมูลบิลหลักไปเป็น Model
    }
  }
}

