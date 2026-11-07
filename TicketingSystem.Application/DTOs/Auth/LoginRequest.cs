using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Application.DTOs.Auth
{
    // [ApiController] checks these attributes and returns a 400 before the action runs
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)] // Hashing is slow on purpose, so don't let huge passwords reach it
        public string Password { get; set; } = string.Empty;
    }
}
