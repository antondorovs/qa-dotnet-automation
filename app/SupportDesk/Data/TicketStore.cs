using Npgsql;
using SupportDesk.Tickets;

namespace SupportDesk.Data;

public class TicketStore
{
    private readonly NpgsqlDataSource dataSource;

    public TicketStore(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task<Ticket> CreateAsync(CreateTicketRequest request)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority
        };
        await using var command = dataSource.CreateCommand("""
            INSERT INTO tickets (id, title, description, priority, status)
            VALUES ($1, $2, $3, $4, $5)
            """);
        command.Parameters.AddWithValue(ticket.Id);
        command.Parameters.AddWithValue(ticket.Title);
        command.Parameters.AddWithValue(ticket.Description);
        command.Parameters.AddWithValue(ticket.Priority);
        command.Parameters.AddWithValue(ticket.Status);
        await command.ExecuteNonQueryAsync();
        return ticket;
    }

    public async Task<Ticket?> GetAsync(Guid id)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, title, description, priority, status FROM tickets WHERE id = $1
            """);
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new Ticket
        {
            Id = reader.GetGuid(0),
            Title = reader.GetString(1),
            Description = reader.GetString(2),
            Priority = reader.GetString(3),
            Status = reader.GetString(4)
        };
    }

    public async Task<bool> ChangeStatusAsync(Guid id, string currentStatus, string newStatus)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE tickets SET status = $1 WHERE id = $2 AND status = $3
            """);
        command.Parameters.AddWithValue(newStatus);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(currentStatus);
        return await command.ExecuteNonQueryAsync() == 1;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM tickets WHERE id = $1");
        command.Parameters.AddWithValue(id);
        return await command.ExecuteNonQueryAsync() == 1;
    }
}
