using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using System;

namespace Stepify.Controllers
{
  [Authorize]
  public class OrderController : Controller
  {
    private readonly StepifyContext _db;
    public OrderController(StepifyContext db) { _db = db; }

    // ==========================================
    // 1. หน้า Checkout ประมวลผลโปรโมชั่นทั้งหมด
    // ==========================================
    public IActionResult Checkout()
    {
      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // 🌟 เพิ่ม 2 บรรทัดนี้ เพื่อดึงที่อยู่ของลูกค้ามาแสดง
      var currentUser = _db.Users.FirstOrDefault(u => u.UserId == currentUserId);
      ViewBag.UserAddress = currentUser?.Address;

      var cartItems = (from c in _db.ShoppingCarts
                       where c.UserId == currentUserId
                       join v in _db.ProductVariants on c.VariantId equals v.VariantId
                       join p in _db.Products on v.ProductId equals p.ProductId
                       select new
                       {
                         c.VariantId,
                         p.Name,
                         Brand = p.Brand ?? "",
                         v.Size,
                         v.Color,
                         p.Price,
                         Quantity = c.Quantity ?? 1,
                         SubTotal = p.Price * (c.Quantity ?? 1)
                       }).ToList();

      if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

      decimal originalTotal = cartItems.Sum(x => x.SubTotal);
      decimal autoDiscountAmount = 0;
      List<string> promoMessages = new List<string>();

      // 🌟 ดึงโปรที่เปิดอยู่ และยังไม่หมดอายุ
      var today = DateTime.Now;
      var activePromos = _db.Promotions
                            .Where(p => p.IsActive == true && (p.ExpiryDate == null || p.ExpiryDate >= today))
                            .ToList();

      // 1. ซื้อ 2 แถม 1 (แถมคู่ถูกสุด)
      if (activePromos.Any(p => p.PromoType == "B2G1"))
      {
        var allShoePrices = new List<decimal>();
        foreach (var item in cartItems)
        {
          for (int i = 0; i < item.Quantity; i++) { allShoePrices.Add(item.Price); }
        }
        allShoePrices = allShoePrices.OrderByDescending(p => p).ToList();
        decimal b2g1Discount = 0;
        for (int i = 2; i < allShoePrices.Count; i += 3) { b2g1Discount += allShoePrices[i]; }
        if (b2g1Discount > 0)
        {
          autoDiscountAmount += b2g1Discount;
          promoMessages.Add($"ซื้อ 2 แถม 1 (ลดไป {b2g1Discount:N0} ฿)");
        }
      }

      // 2. ลดเฉพาะแบรนด์
      var brandPromos = activePromos.Where(p => p.PromoType == "Brand").ToList();
      decimal brandDiscountTotal = 0;
      foreach (var item in cartItems)
      {
        var bp = brandPromos.FirstOrDefault(p => p.ConditionValue != null && p.ConditionValue.ToLower() == item.Brand.ToLower());
        if (bp != null)
        {
          brandDiscountTotal += bp.IsPercent == true ? (item.SubTotal * ((bp.DiscountValue ?? 0) / 100m)) : ((bp.DiscountValue ?? 0) * item.Quantity);
        }
      }
      if (brandDiscountTotal > 0)
      {
        autoDiscountAmount += brandDiscountTotal;
        promoMessages.Add($"ส่วนลดแบรนด์พิเศษ (-{brandDiscountTotal:N0} ฿)");
      }

      // 3. ซื้อครบ X ลด Y (Threshold)
      decimal totalBeforeThreshold = originalTotal - autoDiscountAmount;
      var thresholdPromos = activePromos.Where(p => p.PromoType == "Threshold").ToList();
      Promotion bestThresholdPromo = null;
      foreach (var p in thresholdPromos)
      {
        if (decimal.TryParse(p.ConditionValue, out decimal minSpend) && totalBeforeThreshold >= minSpend)
        {
          if (bestThresholdPromo == null || p.DiscountValue > bestThresholdPromo.DiscountValue) { bestThresholdPromo = p; }
        }
      }
      if (bestThresholdPromo != null)
      {
        decimal discount = bestThresholdPromo.IsPercent == true ? (totalBeforeThreshold * ((bestThresholdPromo.DiscountValue ?? 0) / 100m)) : (bestThresholdPromo.DiscountValue ?? 0);
        autoDiscountAmount += discount;
        promoMessages.Add($"{bestThresholdPromo.PromoName} (-{discount:N0} ฿)");
      }

      // 4. ส่งฟรี
      if (activePromos.Any(p => p.PromoType == "Shipping")) { promoMessages.Add("ส่งฟรีไม่มีขั้นต่ำ 🚚"); }

      ViewBag.AutoPromoMessages = promoMessages;
      ViewBag.AutoDiscountAmount = autoDiscountAmount;
      ViewBag.NetBeforeManualVoucher = originalTotal - autoDiscountAmount;

      // 5. คูปองสำหรับกดใช้งานเอง
      ViewBag.MyVouchers = _db.UserVouchers.Where(v => v.UserId == currentUserId && v.IsUsed == false).ToList();
      ViewBag.ActivePromotions = activePromos.Where(p => p.PromoType == "Coupon").ToList();

      ViewBag.CartItems = cartItems;
      ViewBag.TotalAmount = originalTotal;

      return View(cartItems);
    }

    [HttpPost]
    public IActionResult PlaceOrder(string ShippingAddress, string PaymentMethod, decimal TotalAmount, decimal DiscountAmount, decimal NetAmount, int? UsedVoucherId, int? UsedPromotionId, bool SaveAddressToProfile = false)
    {
      // 🌟 1. ดักจับ Error Server-side ป้องกันที่อยู่จัดส่งเป็นค่าว่าง 
      if (string.IsNullOrWhiteSpace(ShippingAddress))
      {
        TempData["ErrorMsg"] = "กรุณาระบุที่อยู่จัดส่งให้ครบถ้วนก่อนยืนยันคำสั่งซื้อ";
        return RedirectToAction("Checkout");
      }

      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // 🌟 2. เพิ่มส่วนนี้: ถ้าติ๊กเซฟที่อยู่ ให้ Update กลับไปที่โปรไฟล์ User ด้วย
      if (SaveAddressToProfile)
      {
        var user = _db.Users.FirstOrDefault(u => u.UserId == currentUserId);
        if (user != null)
        {
          user.Address = ShippingAddress;
          _db.Users.Update(user);
          // ไม่ต้องรีบ SaveChanges ตรงนี้ เดี๋ยวไป Save พร้อมกันตอนจบบิลทีเดียว
        }
      }
      var newOrder = new Order
      {
        UserId = currentUserId,
        OrderDate = DateTime.Now,
        TotalAmount = TotalAmount,
        PromoDiscount = DiscountAmount,
        NetAmount = NetAmount,
        PaymentStatus = (PaymentMethod == "COD") ? "Pending" : "Paid",
        ShippingStatus = "Packing",
        ShippingAddress = ShippingAddress
      };

      _db.Orders.Add(newOrder);
      _db.SaveChanges();

      var cartItems = _db.ShoppingCarts.Where(c => c.UserId == currentUserId).ToList();
      foreach (var item in cartItems)
      {
        var variant = _db.ProductVariants.FirstOrDefault(v => v.VariantId == item.VariantId);
        if (variant != null)
        {
          var product = _db.Products.FirstOrDefault(p => p.ProductId == variant.ProductId);
          if (product != null)
          {
            int buyQty = item.Quantity ?? 1;
            _db.OrderDetails.Add(new OrderDetail
            {
              OrderId = newOrder.OrderId,
              VariantId = item.VariantId,
              Quantity = buyQty,
              UnitPrice = product.Price,
              SubTotal = product.Price * buyQty
            });
            variant.StockQty = (variant.StockQty ?? 0) - buyQty;
            _db.ProductVariants.Update(variant);
          }
        }
      }

      // ทำลายคูปองส่วนตัวเมื่อใช้แล้ว
      if (UsedVoucherId.HasValue)
      {
        var usedVoucher = _db.UserVouchers.FirstOrDefault(v => v.VoucherId == UsedVoucherId.Value);
        if (usedVoucher != null) { usedVoucher.IsUsed = true; _db.UserVouchers.Update(usedVoucher); }
      }

      _db.ShoppingCarts.RemoveRange(cartItems);
      _db.SaveChanges();
      return RedirectToAction("Success", new { orderId = newOrder.OrderId });
    }

    public IActionResult Success(int orderId) { ViewBag.OrderId = orderId; return View(); }

    public IActionResult History()
    {
      return View(_db.Orders.Where(o => o.UserId == int.Parse(User.FindFirst("UserId").Value)).OrderByDescending(o => o.OrderDate).ToList());
    }

    public IActionResult Details(int id)
    {
      int currentUserId = int.Parse(User.FindFirst("UserId").Value);
      var order = _db.Orders.FirstOrDefault(o => o.OrderId == id && o.UserId == currentUserId);
      if (order == null) return RedirectToAction("History");

      ViewBag.OrderDetails = (from od in _db.OrderDetails
                              where od.OrderId == id
                              join v in _db.ProductVariants on od.VariantId equals v.VariantId
                              join p in _db.Products on v.ProductId equals p.ProductId
                              let img = _db.ProductImages.FirstOrDefault(i => i.ProductId == p.ProductId && i.IsPrimary == true)
                              select new { p.Name, v.Size, v.Color, od.UnitPrice, od.Quantity, od.SubTotal, ImageUrl = img != null ? img.ImageUrl : "https://via.placeholder.com/100" }).ToList();
      return View(order);
    }
    // 🌟 เมธอดสำหรับรับค่าจากปุ่ม "บันทึกที่อยู่ลงโปรไฟล์" ผ่าน AJAX
    [HttpPost]
    public IActionResult UpdateProfileAddress(string newAddress)
    {
        try 
        {
            int currentUserId = int.Parse(User.FindFirst("UserId").Value);
            var user = _db.Users.FirstOrDefault(u => u.UserId == currentUserId);
            
            if (user != null)
            {
                user.Address = newAddress;
                _db.Users.Update(user);
                _db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "ไม่พบข้อมูลผู้ใช้" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
  }
}