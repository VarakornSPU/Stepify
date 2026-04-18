using System.ComponentModel.DataAnnotations;

namespace Stepify.Models.ViewModels
{
    public class ReviewViewModel
    {
        [Required]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "กรุณาให้คะแนน")]
        [Range(1, 5, ErrorMessage = "คะแนนต้องอยู่ระหว่าง 1-5")]
        public int Rating { get; set; }

        [StringLength(500, ErrorMessage = "คอมเมนต์ต้องไม่เกิน 500 ตัวอักษร")]
        public string Comment { get; set; }
    }
}