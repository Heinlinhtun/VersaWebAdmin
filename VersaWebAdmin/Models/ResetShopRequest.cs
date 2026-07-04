using System.ComponentModel.DataAnnotations;

namespace VersaWebAdmin.Models;

public class ResetShopRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string AdminPassword { get; set; } = string.Empty;

    [Required]
    public string ShopAdminUsername { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string NewShopAdminPassword { get; set; } = string.Empty;
}
