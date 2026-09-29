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

    public async Task<List<Ticket>> ListAsync(string search, string priority, string status)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, title, description, priority, status FROM tickets
            WHERE ($1 = '' OR strpos(lower(title), lower($1)) > 0
                OR strpos(lower(description), lower($1)) > 0)
              AND ($2 = '' OR lower(priority) = lower($2))
              AND ($3 = '' OR lower(status) = lower($3))
            ORDER BY title, id
            """);
        command.Parameters.AddWithValue(search);
        command.Parameters.AddWithValue(priority);
        command.Parameters.AddWithValue(status);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<Ticket>();
        while (await reader.ReadAsync())
        {
            result.Add(new Ticket
            {
                Id = reader.GetGuid(0),
                Title = reader.GetString(1),
                Description = reader.GetString(2),
                Priority = reader.GetString(3),
                Status = reader.GetString(4)
            });
        }
        return result;
    }

    public async Task<bool> UpdateAsync(Guid id, CreateTicketRequest request)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE tickets SET title = $1, description = $2, priority = $3 WHERE id = $4
            """);
        command.Parameters.AddWithValue(request.Title.Trim());
        command.Parameters.AddWithValue(request.Description.Trim());
        command.Parameters.AddWithValue(request.Priority);
        command.Parameters.AddWithValue(id);
        return await command.ExecuteNonQueryAsync() == 1;
    }

    public async Task<Comment?> AddCommentAsync(Guid ticketId, string body)
    {
        var comment = new Comment { Id = Guid.NewGuid(), Body = body.Trim() };
        await using var command = dataSource.CreateCommand("""
            INSERT INTO comments (id, ticket_id, body)
            SELECT $1, id, $2 FROM tickets WHERE id = $3
            """);
        command.Parameters.AddWithValue(comment.Id);
        command.Parameters.AddWithValue(comment.Body);
        command.Parameters.AddWithValue(ticketId);
        return await command.ExecuteNonQueryAsync() == 1 ? comment : null;
    }

    public async Task<List<Comment>> GetCommentsAsync(Guid ticketId)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, body FROM comments WHERE ticket_id = $1 ORDER BY created_at, id
            """);
        command.Parameters.AddWithValue(ticketId);
        await using var reader = await command.ExecuteReaderAsync();
        var comments = new List<Comment>();
        while (await reader.ReadAsync())
        {
            comments.Add(new Comment { Id = reader.GetGuid(0), Body = reader.GetString(1) });
        }
        return comments;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM tickets WHERE id = $1");
        command.Parameters.AddWithValue(id);
        return await command.ExecuteNonQueryAsync() == 1;
    }
}
