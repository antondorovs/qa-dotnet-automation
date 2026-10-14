namespace SupportDesk.Tickets;

public class Comment
{
    public Guid Id { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
