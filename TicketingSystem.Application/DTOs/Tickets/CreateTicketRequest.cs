using System.ComponentModel.DataAnnotations;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.DTOs.Tickets
{
    // The lengths match the column sizes in the EF configurations, so a value that
    // passes here can't fail at the database with a 500
    public class CreateTicketRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        [RegularExpression(@"^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/?$",
            ErrorMessage = "The repository link must look like https://github.com/owner/repo.")]
        public string RepositoryUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Branch { get; set; } = string.Empty;

        // 7 is the shortest SHA git normally shows, 64 is a full SHA-256
        [Required]
        [RegularExpression("^[0-9a-fA-F]{7,64}$",
            ErrorMessage = "CommitSha must be 7 to 64 hexadecimal characters.")]
        public string CommitSha { get; set; } = string.Empty;

        // JSON accepts any number for an enum (even 99), so this checks the value really exists
        [EnumDataType(typeof(Priority))]
        public Priority Priority { get; set; } = Priority.Medium;
    }
}
