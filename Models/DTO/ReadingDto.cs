namespace test_project.Models.DTO;

public class ReadingDto
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string? Username { get; set; }
    public int SpreadId { get; set; }
    public string? SpreadName { get; set; }
    public string? Question { get; set; }
    public string Status { get; set; } = "NEW";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ReadingCardDto> Cards { get; set; } = new();
}

public class ReadingCardDto
{
    public long Id { get; set; }
    public int CardId { get; set; }
    public string CardName { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool IsReversed { get; set; }
}

public class CreateReadingDto
{
    public int UserId { get; set; }
    public int SpreadId { get; set; }
    public string? Question { get; set; }
    public string Status { get; set; } = "NEW";
    public List<CreateReadingCardDto>? Cards { get; set; }
}

public class CreateReadingCardDto
{
    public int CardId { get; set; }
    public int Position { get; set; }
    public bool IsReversed { get; set; }
}

public class UpdateReadingDto
{
    public string? Question { get; set; }
    public string Status { get; set; } = "NEW";
}

public class ReadingQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? UserId { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Sort { get; set; } = "-created_at";
}
