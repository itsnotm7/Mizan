namespace TicketingSystem.Domain.Enums
{
    // Every step a user can take on a ticket. Each one maps to one rule in TicketWorkflow.
    public enum WorkflowAction
    {
        Route, Reject,
        Assign, Submit,
        LeaderApprove, LeaderSendBack,
        ManagerApprove, ManagerSendBack,
        Close, Reopen
    }
}
