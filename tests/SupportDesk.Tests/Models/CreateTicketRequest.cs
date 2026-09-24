namespace QaDotnetWorkflows.Tests.Models;

public class CreateTicketRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "Normal";
}
