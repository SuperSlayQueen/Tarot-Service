namespace test_project.Models.Entities;

public class Card
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Suit { get; set; } = string.Empty;
    public int? Number { get; set; }
    public string? UprightMeaning { get; set; }
    public string? ReversedMeaning { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int? DeckId { get; set; }
    public Deck? Deck { get; set; }

    public ICollection<SpreadCard> SpreadCards { get; set; } = new List<SpreadCard>();
}
