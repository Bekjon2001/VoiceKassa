using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("VOICEKASSA_CONNECTION_STRING")
    ?? throw new InvalidOperationException("The private connection string was not provided.");
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

if (args.Contains("--apply", StringComparer.Ordinal))
{
    var migrationSql = await File.ReadAllTextAsync(
        "src\\VoiceKassa.DataLayer\\Migrations\\20261114_StaffAccounts.sql");
    await using var migration = new NpgsqlCommand(migrationSql, connection);
    await migration.ExecuteNonQueryAsync();
    Console.WriteLine("Staff accounts migration applied.");
}

await using (var command = new NpgsqlCommand("SELECT COUNT(*) FROM voicekassa.\"STAFF\"", connection))
    Console.WriteLine($"Staff rows: {await command.ExecuteScalarAsync()}");

const string columnsSql = """
    SELECT column_name
    FROM information_schema.columns
    WHERE table_schema = 'voicekassa'
      AND table_name = 'STAFF'
      AND column_name IN ('LOGIN', 'PASSWORD_HASH', 'ACCESS_TOKEN')
    ORDER BY column_name
    """;
await using (var command = new NpgsqlCommand(columnsSql, connection))
await using (var reader = await command.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
        Console.WriteLine($"Existing staff column: {reader.GetString(0)}");
}
