namespace QaDotnetWorkflows.Tests.Models;

public class CommentResponse
{
    public Guid Id { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
