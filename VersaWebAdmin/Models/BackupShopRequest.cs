using System.ComponentModel.DataAnnotations;

namespace VersaWebAdmin.Models;

public class BackupShopRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string AdminPassword { get; set; } = string.Empty;
}

public class DuplicateShopRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string AdminPassword { get; set; } = string.Empty;

    [Required]
    public string NewShopName { get; set; } = string.Empty;

    [Required]
    public string NewShopLoginId { get; set; } = string.Empty;

    [Required]
    public string NewAdminUsername { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string NewAdminPassword { get; set; } = string.Empty;
}
