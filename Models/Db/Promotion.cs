using System;
using System.Collections.Generic;

namespace Stepify.Models.Db;

public partial class Promotion
{
    public int PromotionId { get; set; }

    public string PromoName { get; set; } = null!;

    public string PromoType { get; set; } = null!;

    public string? ConditionValue { get; set; }

    public decimal? DiscountValue { get; set; }

    public bool? IsPercent { get; set; }

    public bool? IsActive { get; set; }
}
