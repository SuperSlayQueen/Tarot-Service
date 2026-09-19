namespace test_project.Models.Entities;

/// <summary>
/// Пользовательский расклад (сессия гадания) — основная растущая сущность для масштабирования.
/// </summary>
public class Reading
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int SpreadId { get; set; }
    public string? Question { get; set; }
    public string Status { get; set; } = "NEW"; // NEW, COMPLETED, CANCELLED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User? User { get; set; }
    public Spread? Spread { get; set; }
    public ICollection<ReadingCard> ReadingCards { get; set; } = new List<ReadingCard>();
}

public class ReadingCard
{
    public long Id { get; set; }
    public long ReadingId { get; set; }
    public int CardId { get; set; }
    public int Position { get; set; }
    public bool IsReversed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Reading? Reading { get; set; }
    public Card? Card { get; set; }
}
