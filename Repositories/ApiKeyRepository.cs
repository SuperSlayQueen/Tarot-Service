using Dapper;
using Npgsql;
using test_project.Models.Entities;
using test_project.Repositories.Interfaces;

namespace test_project.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly string _connectionString;

    public ApiKeyRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ApiKey?> GetByKeyAsync(string key)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT id, key, name, is_active, created_at, expires_at
            FROM api_keys
            WHERE key = @Key AND is_active = true AND expires_at > CURRENT_TIMESTAMP";

        return await connection.QueryFirstOrDefaultAsync<ApiKey>(sql, new { Key = key });
    }

    public async Task<ApiKey?> GetByIdAsync(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT id, key, name, is_active, created_at, expires_at
            FROM api_keys
            WHERE id = @Id";

        return await connection.QueryFirstOrDefaultAsync<ApiKey>(sql, new { Id = id });
    }

    public async Task<List<ApiKey>> GetAllAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT id, key, name, is_active, created_at, expires_at
            FROM api_keys
            ORDER BY created_at DESC";

        var result = await connection.QueryAsync<ApiKey>(sql);
        return result.ToList();
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = @"
                INSERT INTO api_keys (key, name, is_active, created_at, expires_at)
                VALUES (@Key, @Name, @IsActive, @CreatedAt, @ExpiresAt)
                RETURNING id, key, name, is_active, created_at, expires_at";

            apiKey.CreatedAt = DateTime.UtcNow;
            var result = await connection.QueryFirstOrDefaultAsync<ApiKey>(
                sql,
                new
                {
                    apiKey.Key,
                    apiKey.Name,
                    apiKey.IsActive,
                    apiKey.CreatedAt,
                    apiKey.ExpiresAt
                },
                transaction);

            await transaction.CommitAsync();
            return result ?? apiKey;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiKey> UpdateAsync(ApiKey apiKey)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = @"
                UPDATE api_keys
                SET key = @Key, name = @Name, is_active = @IsActive, expires_at = @ExpiresAt
                WHERE id = @Id
                RETURNING id, key, name, is_active, created_at, expires_at";

            var result = await connection.QueryFirstOrDefaultAsync<ApiKey>(
                sql,
                new
                {
                    apiKey.Id,
                    apiKey.Key,
                    apiKey.Name,
                    apiKey.IsActive,
                    apiKey.ExpiresAt
                },
                transaction);

            await transaction.CommitAsync();
            return result ?? apiKey;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = "DELETE FROM api_keys WHERE id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id }, transaction);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
