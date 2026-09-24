using QaDotnetWorkflows.Tests.Models;

namespace QaDotnetWorkflows.Tests.TestData;

public static class TicketData
{
    public static CreateTicketRequest NewTicket(string priority = "Normal")
    {
        return new CreateTicketRequest
        {
            Title = $"Delivery inquiry {Guid.NewGuid():N}",
            Description = "The customer needs an updated delivery date.",
            Priority = priority
        };
    }
}
