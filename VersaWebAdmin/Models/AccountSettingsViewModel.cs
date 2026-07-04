using System.ComponentModel.DataAnnotations;

namespace VersaWebAdmin.Models;

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6, ErrorMessage = "New password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class CreateUserViewModel
{
    [Required]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters.")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Admin";
}

public class MasterUser
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class AccountSettingsViewModel
{
    public ChangePasswordViewModel ChangePassword { get; set; } = new();
    public CreateUserViewModel CreateUser { get; set; } = new();
    public List<MasterUser> Users { get; set; } = new();
}
