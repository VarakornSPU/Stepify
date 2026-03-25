using System;
using System.Collections.Generic;

namespace Stepify.Models.Db;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? Tel { get; set; }

    public string? Role { get; set; }

    public string? Address { get; set; }

    public DateTime? CreatedAt { get; set; }
}
