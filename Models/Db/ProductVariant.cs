using System;
using System.Collections.Generic;

namespace Stepify.Models.Db;

public partial class ProductVariant
{
    public int VariantId { get; set; }

    public int ProductId { get; set; }

    public string Size { get; set; } = null!;

    public string? Color { get; set; }

    public int? StockQty { get; set; }
}
