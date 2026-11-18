using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Application.DTOs.Tickets
{
    public class RouteTicketRequest
    {
        // Nullable so a missing teamId fails [Required] instead of arriving as an empty Guid
        [Required]
        public Guid? TeamId { get; set; }
    }

    public class AssignMembersRequest
    {
        // May be empty when continuing a reopened ticket with the members already assigned
        [Required]
        [MaxLength(20)]
        public List<Guid> MemberIds { get; set; } = [];
    }

    public class SubmitFixRequest
    {
        [Required]
        [MaxLength(200)]
        public string Branch { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[0-9a-fA-F]{7,64}$",
            ErrorMessage = "The commit SHA must be 7 to 64 hexadecimal characters.")]
        public string CommitSha { get; set; } = string.Empty;

        // What was fixed: this becomes the response the client receives once approved
        [Required]
        [MaxLength(4000)]
        public string Summary { get; set; } = string.Empty;
    }
}
