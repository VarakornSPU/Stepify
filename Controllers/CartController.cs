using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace Stepify.Controllers
{
  // บังคับว่าต้อง Login ก่อนถึงจะใช้งานระบบตะกร้าได้!
  [Authorize]
  public class CartController : Controller
  {
    private readonly StepifyContext _db;

    public CartController(StepifyContext db)
    {
      _db = db;
    }

    // ==========================================
    // 1. หน้าแสดงตะกร้าสินค้า (View Cart)
    // ==========================================
    public IActionResult Index()
    {
      // ดึง UserId ของคนที่ล็อกอินอยู่ปัจจุบันจาก Cookie
      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // ดึงข้อมูลจากตาราง ShoppingCarts มา Join กับ สินค้า และ ไซส์ เพื่อแสดงผล
      var cartItems = (from c in _db.ShoppingCarts
                       where c.UserId == currentUserId
                       join v in _db.ProductVariants on c.VariantId equals v.VariantId
                       join p in _db.Products on v.ProductId equals p.ProductId
                       // หารูปหน้าปก
                       let img = _db.ProductImages.FirstOrDefault(i => i.ProductId == p.ProductId && i.IsPrimary == true)
                       select new
                       {
                         c.CartId,
                         p.ProductId,
                         p.Name,
                         v.Size,
                         v.Color,
                         p.Price,
                         c.Quantity,
                         SubTotal = p.Price * c.Quantity, // ราคารวมของแต่ละรายการ
                         ImageUrl = img != null ? img.ImageUrl : "https://via.placeholder.com/150"
                       }).ToList();

      ViewBag.CartItems = cartItems;

      // คำนวณยอดรวมทั้งหมดในตะกร้า
      ViewBag.TotalAmount = cartItems.Sum(x => x.SubTotal);

      return View();
    }

    // ==========================================
    // 2. ฟังก์ชันรับค่าจากหน้า Details เพื่อเพิ่มลงตะกร้า
    // ==========================================
    [HttpPost]
    public IActionResult AddToCart(int VariantId)
    {
      int currentUserId = int.Parse(User.FindFirst("UserId").Value);

      // เช็คว่าในตะกร้ามีสินค้ารหัสนี้ (ไซส์นี้ สีนี้) ของ User คนนี้อยู่แล้วหรือยัง?
      var existingCart = _db.ShoppingCarts.FirstOrDefault(c => c.UserId == currentUserId && c.VariantId == VariantId);

      if (existingCart != null)
      {
        // ถ้ามีอยู่แล้ว ให้บวกจำนวนเพิ่มไปอีก 1
        existingCart.Quantity += 1;
        _db.ShoppingCarts.Update(existingCart);
      }
      else
      {
        // ถ้ายังไม่มี ให้สร้างรายการใหม่ในตะกร้า
        var newCartItem = new ShoppingCart
        {
          UserId = currentUserId,
          VariantId = VariantId,
          Quantity = 1 // เริ่มที่ 1 คู่
        };
        _db.ShoppingCarts.Add(newCartItem);
      }

      _db.SaveChanges();

      // เพิ่มเสร็จ เด้งไปหน้าดูตะกร้าสินค้า
      return RedirectToAction("Index");
    }

    // ==========================================
    // 3. ฟังก์ชันลบสินค้าออกจากตะกร้า
    // ==========================================
    public IActionResult RemoveFromCart(int id)
    {
      var cartItem = _db.ShoppingCarts.FirstOrDefault(c => c.CartId == id);
      if (cartItem != null)
      {
        _db.ShoppingCarts.Remove(cartItem);
        _db.SaveChanges();
      }
      return RedirectToAction("Index");
    }
    // ==========================================
    // 4. ฟังก์ชันอัปเดตจำนวนสินค้า (เพิ่ม/ลด)
    // ==========================================
    [HttpPost]
    public IActionResult UpdateQuantity(int cartId, int change)
    {
      var cartItem = _db.ShoppingCarts.FirstOrDefault(c => c.CartId == cartId);
      if (cartItem != null)
      {
        // คำนวณจำนวนใหม่
        int newQuantity = (cartItem.Quantity ?? 1) + change;

        if (newQuantity > 0)
        {
          // เช็ค Stock ก่อนเพิ่ม (Option)
          var variant = _db.ProductVariants.FirstOrDefault(v => v.VariantId == cartItem.VariantId);
          if (variant != null && newQuantity <= (variant.StockQty ?? 0))
          {
            cartItem.Quantity = newQuantity;
            _db.ShoppingCarts.Update(cartItem);
          }
          else if (change < 0) // กรณีลดจำนวน ไม่ต้องเช็คสต็อกเพิ่ม
          {
            cartItem.Quantity = newQuantity;
            _db.ShoppingCarts.Update(cartItem);
          }
        }
        else
        {
          // ถ้าลดจนเหลือ 0 ให้ลบทิ้ง
          _db.ShoppingCarts.Remove(cartItem);
        }
        _db.SaveChanges();
      }
      return RedirectToAction("Index");
    }
  }
}