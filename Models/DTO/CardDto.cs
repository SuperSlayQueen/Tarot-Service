namespace test_project.Models.DTO;

public class CardDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Suit { get; set; } = string.Empty;
    public int? Number { get; set; }
    public string? UprightMeaning { get; set; }
    public string? ReversedMeaning { get; set; }
    public int? DeckId { get; set; }
    public string? DeckName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCardDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Suit { get; set; } = string.Empty;
    public int? Number { get; set; }
    public string? UprightMeaning { get; set; }
    public string? ReversedMeaning { get; set; }
    public int? DeckId { get; set; }
}

public class UpdateCardDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Suit { get; set; } = string.Empty;
    public int? Number { get; set; }
    public string? UprightMeaning { get; set; }
    public string? ReversedMeaning { get; set; }
    public int? DeckId { get; set; }
}
