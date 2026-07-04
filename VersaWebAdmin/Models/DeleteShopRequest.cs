using System.ComponentModel.DataAnnotations;

namespace VersaWebAdmin.Models
{
    public class DeleteShopRequest
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string AdminPassword { get; set; } = string.Empty;
    }
}