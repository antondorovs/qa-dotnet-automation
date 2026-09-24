namespace SupportDesk.Tickets;

public static class TicketValidation
{
    public static Dictionary<string, string[]> Validate(CreateTicketRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 120)
        {
            errors["title"] = new[] { "Title must contain 1 to 120 characters." };
        }
        if (request.Description is null || request.Description.Length > 2000)
        {
            errors["description"] = new[] { "Description must contain at most 2000 characters." };
        }
        if (request.Priority != "Low" && request.Priority != "Normal" && request.Priority != "High")
        {
            errors["priority"] = new[] { "Priority must be Low, Normal or High." };
        }
        return errors;
    }
}
