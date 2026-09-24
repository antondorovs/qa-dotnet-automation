using Npgsql;
using SupportDesk.Data;
using SupportDesk.Tickets;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SupportDesk")
    ?? throw new InvalidOperationException("ConnectionStrings__SupportDesk is required.");
await using var dataSource = NpgsqlDataSource.Create(connectionString);
var tickets = new TicketStore(dataSource);
var app = builder.Build();

app.MapGet("/health", async () =>
{
    await using var command = dataSource.CreateCommand("SELECT 1");
    await command.ExecuteScalarAsync();
    return Results.Ok(new { status = "ready" });
});

app.MapPost("/api/tickets", async (CreateTicketRequest request) =>
{
    var errors = TicketValidation.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }
    var ticket = await tickets.CreateAsync(request);
    return Results.Created($"/api/tickets/{ticket.Id}", ticket);
});

app.MapGet("/api/tickets/{id:guid}", async (Guid id) =>
{
    var ticket = await tickets.GetAsync(id);
    return ticket is null
        ? Results.NotFound(new { error = "Ticket not found." })
        : Results.Ok(ticket);
});

app.MapPatch("/api/tickets/{id:guid}/status", async (Guid id, ChangeStatusRequest request) =>
{
    var ticket = await tickets.GetAsync(id);
    if (ticket is null)
    {
        return Results.NotFound(new { error = "Ticket not found." });
    }
    var allowed = (ticket.Status == "Open" && request.Status == "InProgress") ||
        (ticket.Status == "InProgress" && request.Status == "Resolved");
    if (!allowed || !await tickets.ChangeStatusAsync(id, ticket.Status, request.Status))
    {
        return Results.Conflict(new { error = "Status transition is not allowed." });
    }
    return Results.Ok(await tickets.GetAsync(id));
});

app.MapDelete("/api/tickets/{id:guid}", async (Guid id) =>
{
    return await tickets.DeleteAsync(id)
        ? Results.NoContent()
        : Results.NotFound(new { error = "Ticket not found." });
});

await app.RunAsync();
