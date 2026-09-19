namespace test_project.Models.DTO;

public class SpreadDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int NumberOfPositions { get; set; }
    public string? PositionNames { get; set; }
    public List<SpreadCardDto> Cards { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SpreadCardDto
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public string CardName { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool IsReversed { get; set; }
}

public class CreateSpreadDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int NumberOfPositions { get; set; }
    public string? PositionNames { get; set; }
}

public class UpdateSpreadDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int NumberOfPositions { get; set; }
    public string? PositionNames { get; set; }
}

public class AddCardToSpreadDto
{
    public int CardId { get; set; }
    public int Position { get; set; }
    public bool IsReversed { get; set; }
}
