using System;
using System.Collections.Generic;

namespace Stepify.Models.Db;

public partial class Product
{
    public int ProductId { get; set; }

    public string Name { get; set; } = null!;

    public string? Brand { get; set; }

    public decimal Price { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }
}
