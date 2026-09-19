using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.DTO;
using test_project.Models.Entities;
using test_project.Repositories.Interfaces;

namespace test_project.Repositories;

public class ReadingRepository : IReadingRepository
{
    private readonly TarotDbContext _write;
    private readonly TarotReadDbContext _read;

    public ReadingRepository(TarotDbContext write, TarotReadDbContext read)
    {
        _write = write;
        _read = read;
    }

    public async Task<Reading?> GetByIdAsync(long id)
    {
        return await _read.Readings
            .Include(r => r.User)
            .Include(r => r.Spread)
            .Include(r => r.ReadingCards)
                .ThenInclude(rc => rc.Card)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<PagedResponseDto<Reading>> GetPagedAsync(ReadingQueryDto query)
    {
        // Lab 4: список readings читаем с Replica
        var q = _read.Readings
            .Include(r => r.User)
            .Include(r => r.Spread)
            .AsQueryable();

        if (query.UserId.HasValue)
            q = q.Where(r => r.UserId == query.UserId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(r => r.Status == query.Status);

        if (query.From.HasValue)
            q = q.Where(r => r.CreatedAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(r => r.CreatedAt < query.To.Value);

        q = (query.Sort?.ToLowerInvariant()) switch
        {
            "created_at" => q.OrderBy(r => r.CreatedAt),
            "-created_at" => q.OrderByDescending(r => r.CreatedAt),
            "status" => q.OrderBy(r => r.Status),
            "-status" => q.OrderByDescending(r => r.Status),
            _ => q.OrderByDescending(r => r.CreatedAt)
        };

        var total = await q.CountAsync();
        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResponseDto<Reading>
        {
            Items = items,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<List<Reading>> GetByUserIdAsync(int userId)
    {
        return await _read.Readings
            .Include(r => r.Spread)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Reading> CreateAsync(Reading reading)
    {
        reading.CreatedAt = DateTime.UtcNow;
        _write.Readings.Add(reading);
        await _write.SaveChangesAsync();
        return reading;
    }

    public async Task<Reading> UpdateAsync(Reading reading)
    {
        reading.UpdatedAt = DateTime.UtcNow;
        _write.Readings.Update(reading);
        await _write.SaveChangesAsync();
        return reading;
    }

    public async Task DeleteAsync(long id)
    {
        var reading = await _write.Readings.FirstOrDefaultAsync(r => r.Id == id);
        if (reading != null)
        {
            _write.Readings.Remove(reading);
            await _write.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(long id)
    {
        return await _write.Readings.AnyAsync(r => r.Id == id);
    }
}
