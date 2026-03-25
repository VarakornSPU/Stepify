using System;
using System.Collections.Generic;

namespace Stepify.Models.Db;

public partial class Review
{
    public int ReviewId { get; set; }

    public int UserId { get; set; }

    public int ProductId { get; set; }

    public int OrderId { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public bool? IsRewardClaimed { get; set; }

    public DateTime? CreatedAt { get; set; }
}
