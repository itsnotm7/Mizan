namespace TicketingSystem.Application.DTOs.Tickets
{
    public record TicketDetailsDto(
        Guid Id,
        string Title,
        string Description,
        string RepositoryUrl,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime? ClosedAt,
        TicketUserDto Client,
        TicketUserDto? Manager,
        Guid? TeamId,
        string? TeamName,
        IReadOnlyList<TicketUserDto> Assignees,
        IReadOnlyList<CodeSnapshotDto> Snapshots);

    // Only the id and name: a ticket screen has no reason to expose emails or roles
    public record TicketUserDto(Guid Id, string Name);

    public record CodeSnapshotDto(
        Guid Id,
        string Kind,
        string RepositoryUrl,
        string Branch,
        string CommitSha,
        DateTime CreatedAt);
}
