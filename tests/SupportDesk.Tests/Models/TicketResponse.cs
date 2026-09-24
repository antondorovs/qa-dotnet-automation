namespace QaDotnetWorkflows.Tests.Models;

public class TicketResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Status { get; set; } = "";
}
