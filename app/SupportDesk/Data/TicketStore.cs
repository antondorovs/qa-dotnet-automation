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
            RETURNING created_at
            """);
        command.Parameters.AddWithValue(ticket.Id);
        command.Parameters.AddWithValue(ticket.Title);
        command.Parameters.AddWithValue(ticket.Description);
        command.Parameters.AddWithValue(ticket.Priority);
        command.Parameters.AddWithValue(ticket.Status);
        ticket.CreatedAt = new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync())!);
        return ticket;
    }

    public async Task<Ticket?> GetAsync(Guid id)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT t.id, t.title, t.description, t.priority, t.status,
              (SELECT count(*)::int FROM comments c WHERE c.ticket_id = t.id),
              t.created_at
            FROM tickets t
            WHERE t.id = $1
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
            Status = reader.GetString(4),
            CommentCount = reader.GetInt32(5),
            CreatedAt = new DateTimeOffset(reader.GetDateTime(6))
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

    public async Task<List<Ticket>> ListAsync(string search, string priority, string status, string sort)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT t.id, t.title, t.description, t.priority, t.status,
              (SELECT count(*)::int FROM comments c WHERE c.ticket_id = t.id),
              t.created_at
            FROM tickets t
            WHERE ($1 = '' OR strpos(lower(title), lower($1)) > 0
                OR strpos(lower(description), lower($1)) > 0)
              AND ($2 = '' OR lower(priority) = lower($2))
              AND ($3 = '' OR lower(status) = lower($3))
            ORDER BY
              CASE WHEN $4 = 'priority' THEN
                CASE priority WHEN 'High' THEN 1 WHEN 'Normal' THEN 2 ELSE 3 END
              END,
              CASE WHEN $4 = 'status' THEN
                CASE status WHEN 'Open' THEN 1 WHEN 'InProgress' THEN 2 ELSE 3 END
              END,
              CASE WHEN $4 = 'comments' THEN
                (SELECT count(*) FROM comments c_order WHERE c_order.ticket_id = t.id)
              END DESC,
              CASE WHEN $4 = 'newest' THEN t.created_at END DESC,
              CASE WHEN $4 = 'oldest' THEN t.created_at END,
              title,
              id
            """);
        command.Parameters.AddWithValue(search);
        command.Parameters.AddWithValue(priority);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(sort);
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
                Status = reader.GetString(4),
                CommentCount = reader.GetInt32(5),
                CreatedAt = new DateTimeOffset(reader.GetDateTime(6))
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
            RETURNING created_at
            """);
        command.Parameters.AddWithValue(comment.Id);
        command.Parameters.AddWithValue(comment.Body);
        command.Parameters.AddWithValue(ticketId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }
        comment.CreatedAt = new DateTimeOffset(reader.GetDateTime(0));
        return comment;
    }

    public async Task<List<Comment>> GetCommentsAsync(Guid ticketId)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, body, created_at FROM comments WHERE ticket_id = $1 ORDER BY created_at, id
            """);
        command.Parameters.AddWithValue(ticketId);
        await using var reader = await command.ExecuteReaderAsync();
        var comments = new List<Comment>();
        while (await reader.ReadAsync())
        {
            comments.Add(new Comment
            {
                Id = reader.GetGuid(0),
                Body = reader.GetString(1),
                CreatedAt = new DateTimeOffset(reader.GetDateTime(2))
            });
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
