using System;
using System.Collections.Generic;

namespace Stepify.Models.Db;

public partial class UserVoucher
{
    public int VoucherId { get; set; }

    public int UserId { get; set; }

    public string VoucherType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public bool? IsPercent { get; set; }

    public bool? IsUsed { get; set; }
}
