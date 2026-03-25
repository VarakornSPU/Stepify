using System;
using System.Collections.Generic;

namespace Stepify.Models.Db;

public partial class ShoppingCart
{
    public int CartId { get; set; }

    public int UserId { get; set; }

    public int VariantId { get; set; }

    public int? Quantity { get; set; }
}
