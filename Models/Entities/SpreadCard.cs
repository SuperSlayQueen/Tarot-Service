namespace test_project.Models.Entities;

public class SpreadCard
{
    public int Id { get; set; }
    public int SpreadId { get; set; }
    public int CardId { get; set; }
    public int Position { get; set; }
    public bool IsReversed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Spread Spread { get; set; } = null!;
    public Card Card { get; set; } = null!;
}
