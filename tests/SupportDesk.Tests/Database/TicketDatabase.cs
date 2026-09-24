using Npgsql;

namespace QaDotnetWorkflows.Tests.Database;

public class TicketDatabase
{
    private readonly string connectionString;

    public TicketDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QA_DatabaseConnection is required for database assertions.");
        }
        this.connectionString = connectionString;
    }

    public async Task<long> CountByDescriptionAsync(string description)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM tickets WHERE description = $1", connection);
        command.Parameters.AddWithValue(description);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Count returned no value."));
    }

    public async Task<string?> GetStatusAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT status FROM tickets WHERE id = $1", connection);
        command.Parameters.AddWithValue(id);
        return await command.ExecuteScalarAsync() as string;
    }
}
