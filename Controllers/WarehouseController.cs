using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace Stepify.Controllers
{
  [Authorize(Roles = "Warehouse, Admin")] // <--- อนาคตถ้ามี Role สามารถปลดคอมเมนต์ตรงนี้ได้เลยครับ
  public class WarehouseController : Controller
  {
    private readonly StepifyContext _db;

    public WarehouseController(StepifyContext db)
    {
      _db = db;
    }

    // ==========================================
    // 1. หน้าจัดการคลังสินค้า (Warehouse - Manage Stock)
    // ==========================================
    public IActionResult ManageStock()
    {
      var stockList = (from v in _db.ProductVariants
                       join p in _db.Products on v.ProductId equals p.ProductId
                       let img = _db.ProductImages.FirstOrDefault(i => i.ProductId == p.ProductId && i.IsPrimary == true)
                       orderby p.Name, v.Size
                       select new
                       {
                         v.VariantId,
                         p.ProductId,
                         p.Name,
                         v.Size,
                         v.Color,
                         v.StockQty,
                         ImageUrl = img != null ? img.ImageUrl : "https://via.placeholder.com/50"
                       }).ToList();

      ViewBag.StockList = stockList;
      return View();
    }

    // ==========================================
    // 2. ฟังก์ชันอัปเดตจำนวนสต๊อก (POST)
    // ==========================================
    [HttpPost]
    public IActionResult UpdateStock(int VariantId, int NewStockQty)
    {
      var variant = _db.ProductVariants.FirstOrDefault(v => v.VariantId == VariantId);
      if (variant != null)
      {
        variant.StockQty = NewStockQty;
        _db.ProductVariants.Update(variant);
        _db.SaveChanges();
      }

      return RedirectToAction("ManageStock");
    }
    // หน้าดูรายการที่ต้องส่งของวันนี้
    public IActionResult PendingShipments()
    {
      // ดึงเฉพาะออเดอร์ที่จ่ายเงินแล้ว (Paid) และยังไม่ได้ส่ง (Packing)
      var orders = _db.Orders
                      .Where(o => o.PaymentStatus == "Paid" && o.ShippingStatus == "Packing")
                      .ToList();
      return View(orders);
    }
  }
}