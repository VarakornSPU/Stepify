using Microsoft.AspNetCore.Mvc;
using Stepify.Models.Db;
using System.Linq;
using System.Threading.Tasks;

namespace Stepify.ViewComponents
{
    public class CartBadgeViewComponent : ViewComponent
    {
        private readonly StepifyContext _db;

        public CartBadgeViewComponent(StepifyContext db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int count = 0;
            if (User.Identity.IsAuthenticated)
            {
                // ดึง UserId จาก Claims
                var userIdClaim = ((System.Security.Claims.ClaimsPrincipal)User).FindFirst("UserId");
                if (userIdClaim != null)
                {
                    int userId = int.Parse(userIdClaim.Value);
                    // นับจำนวนรายการสินค้าในตะกร้า (หรือใช้ .Sum(c => c.Quantity) ถ้าต้องการนับจำนวนชิ้น)
                    count = _db.ShoppingCarts.Where(c => c.UserId == userId).Count();
                }
            }
            return View(count);
        }
    }
}