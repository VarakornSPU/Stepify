using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace Stepify.Controllers
{
  [Authorize(Roles = "Warehouse, Admin")]
  public class WarehouseController : Controller
  {
    private readonly StepifyContext _db;

    public WarehouseController(StepifyContext db)
    {
      _db = db;
    }

    public IActionResult Index()
    {
      // ให้เด้งไปหน้าจัดการสต็อกอัตโนมัติ
      return RedirectToAction("ManageStock");
    }

    // ==========================================
    // 🌟 ส่วนของเดิม: จัดการสต็อกสินค้า (ที่หายไป)
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

    // ==========================================
    // 🌟 ส่วนใหม่: รายการคำสั่งซื้อที่ต้องจัดส่ง
    // ==========================================
    public IActionResult ManageOrders()
    {
      var orders = _db.Orders
          .Where(o => (o.PaymentStatus == "Paid" || (o.PaymentStatus == "Pending" && o.ShippingStatus == "Packing"))
                      && o.ShippingStatus != "Delivered"
                      && o.ShippingStatus != "Cancelled"
                      & o.ShippingStatus != "Completed")
          .OrderBy(o => o.OrderDate)
          .ToList();

      return View(orders);
    }

    public IActionResult OrderDetails(int id)
    {
      var order = _db.Orders.FirstOrDefault(o => o.OrderId == id);
      if (order == null) return RedirectToAction("ManageOrders");

      ViewBag.OrderDetails = (from od in _db.OrderDetails
                              where od.OrderId == id
                              join v in _db.ProductVariants on od.VariantId equals v.VariantId
                              join p in _db.Products on v.ProductId equals p.ProductId
                              let img = _db.ProductImages.FirstOrDefault(i => i.ProductId == p.ProductId && i.IsPrimary == true)
                              select new
                              {
                                p.Name,
                                v.Size,
                                v.Color,
                                od.Quantity,
                                ImageUrl = img != null ? img.ImageUrl : "https://via.placeholder.com/50"
                              }).ToList();

      ViewBag.Customer = _db.Users.FirstOrDefault(u => u.UserId == order.UserId);
      return View(order);
    }

[HttpPost]
public IActionResult UpdateShippingStatus(int OrderId, string ShippingStatus, string TrackingNumber)
{
    var order = _db.Orders.FirstOrDefault(o => o.OrderId == OrderId);
    if (order != null)
    {
        // คืนสต๊อกเมื่อฝ่ายคลังกดยกเลิก
        if (ShippingStatus == "Cancelled" && order.ShippingStatus != "Cancelled")
        {
            var orderDetails = _db.OrderDetails.Where(od => od.OrderId == OrderId).ToList();
            foreach (var item in orderDetails)
            {
                var variant = _db.ProductVariants.FirstOrDefault(v => v.VariantId == item.VariantId);
                if (variant != null)
                {
                    variant.StockQty = (variant.StockQty ?? 0) + item.Quantity;
                    _db.ProductVariants.Update(variant);
                }
            }
        }

        order.ShippingStatus = ShippingStatus;
        if (!string.IsNullOrEmpty(TrackingNumber))
        {
            order.TrackingNumber = TrackingNumber;
        }
        
        // ถ้าคลังกดยกเลิก อาจจะต้องอัปเดตสถานะการจ่ายเงินด้วย
        if (ShippingStatus == "Cancelled")
        {
            order.PaymentStatus = "Cancelled"; 
        }

        _db.Orders.Update(order);
        _db.SaveChanges();
        TempData["SuccessMsg"] = "อัปเดตสถานะการจัดส่งเรียบร้อยแล้ว";
    }
    return RedirectToAction("ManageOrders");
}

    [HttpPost]
    public IActionResult ReportIssue(string Title, string Description)
    {
      var newIssue = new IssueReport
      {
        Title = Title,
        Description = Description,
        ReportedBy = User.Identity.Name,
        CreatedAt = DateTime.Now,
        Status = "Pending"
      };

      _db.IssueReports.Add(newIssue);
      _db.SaveChanges();

      TempData["SuccessMsg"] = "ส่งแจ้งปัญหาไปยัง Admin เรียบร้อยแล้ว";
      return RedirectToAction("ManageStock");
    }
  }
}