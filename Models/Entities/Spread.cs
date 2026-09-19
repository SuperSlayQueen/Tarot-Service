namespace test_project.Models.Entities;

public class Spread
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int NumberOfPositions { get; set; }
    public string? PositionNames { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<SpreadCard> SpreadCards { get; set; } = new List<SpreadCard>();
}
