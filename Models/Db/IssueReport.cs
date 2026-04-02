using System;
using System.ComponentModel.DataAnnotations; // 🌟 1. เพิ่ม using นี้เข้ามา

namespace Stepify.Models.Db;

public partial class IssueReport
{
    [Key] // 🌟 2. เพิ่ม [Key] ไว้ตรงนี้เพื่อบอก EF Core ว่านี่คือ Primary Key
    public int IssueId { get; set; }
    
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public string ReportedBy { get; set; } = null!;
}