using System;
using System.Collections.Generic;

namespace Stepify.Models.Db;

public partial class Order
{
    public int OrderId { get; set; }

    public int UserId { get; set; }

    public DateTime? OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal? PromoDiscount { get; set; }

    public decimal? VoucherDiscount { get; set; }

    public decimal? ShippingFee { get; set; }

    public decimal NetAmount { get; set; }

    public string? PaymentStatus { get; set; }

    public string? ShippingStatus { get; set; }

    public string? TrackingNumber { get; set; }

    public string ShippingAddress { get; set; } = null!;
}
