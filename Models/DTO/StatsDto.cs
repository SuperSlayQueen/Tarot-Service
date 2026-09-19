namespace test_project.Models.DTO;

public class CardsBySuitDto
{
    public string Suit { get; set; } = string.Empty;
    public int CardsCount { get; set; }
    public int DecksCount { get; set; }
}

public class ReadingsByStatusDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DeckUsageDto
{
    public int DeckId { get; set; }
    public string DeckName { get; set; } = string.Empty;
    public int CardsCount { get; set; }
    public int ReadingsCount { get; set; }
}

public class SpreadStatsDto
{
    public int SpreadId { get; set; }
    public string SpreadName { get; set; } = string.Empty;
    public int ReadingsCount { get; set; }
    public double AvgCardsPerReading { get; set; }
}
