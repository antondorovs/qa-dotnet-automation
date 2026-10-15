namespace SupportDesk.Tickets;

public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Open";
    public int CommentCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
