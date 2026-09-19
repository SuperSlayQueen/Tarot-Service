using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.Entities;
using test_project.Repositories;
using Xunit;

namespace test_project.Tests;

public class CardRepositoryTests : IDisposable
{
    private readonly TarotDbContext _context;
    private readonly CardRepository _repository;

    public CardRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TarotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TarotDbContext(options);
        _repository = new CardRepository(_context);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingCard_ReturnsCard()
    {
        var deck = new Deck { Name = "Test Deck", Description = "Test" };
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        var card = new Card
        {
            Name = "Test Card",
            Description = "Test Description",
            Suit = "Test Suit",
            DeckId = deck.Id
        };
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(card.Id);

        Assert.NotNull(result);
        Assert.Equal(card.Id, result.Id);
        Assert.Equal("Test Card", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingCard_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ValidCard_CreatesCard()
    {
        var deck = new Deck { Name = "Test Deck" };
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        var card = new Card
        {
            Name = "New Card",
            Description = "Description",
            Suit = "Suit",
            DeckId = deck.Id
        };

        var result = await _repository.CreateAsync(card);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("New Card", result.Name);
        var savedCard = await _context.Cards.FindAsync(result.Id);
        Assert.NotNull(savedCard);
    }

    [Fact]
    public async Task UpdateAsync_ExistingCard_UpdatesCard()
    {
        var deck = new Deck { Name = "Test Deck" };
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        var card = new Card
        {
            Name = "Original Name",
            Description = "Original Description",
            Suit = "Suit",
            DeckId = deck.Id
        };
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        card.Name = "Updated Name";
        card.Description = "Updated Description";

        var result = await _repository.UpdateAsync(card);

        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Updated Description", result.Description);
        var updatedCard = await _context.Cards.FindAsync(card.Id);
        Assert.NotNull(updatedCard);
        Assert.Equal("Updated Name", updatedCard.Name);
    }

    [Fact]
    public async Task DeleteAsync_ExistingCard_DeletesCard()
    {
        var card = new Card
        {
            Name = "Card to Delete",
            Description = "Description",
            Suit = "Suit"
        };
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();
        var cardId = card.Id;

        await _repository.DeleteAsync(cardId);

        var deletedCard = await _context.Cards.FindAsync(cardId);
        Assert.Null(deletedCard);
    }

    [Fact]
    public async Task ExistsAsync_ExistingCard_ReturnsTrue()
    {
        var card = new Card
        {
            Name = "Existing Card",
            Description = "Description",
            Suit = "Suit"
        };
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        var result = await _repository.ExistsAsync(card.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingCard_ReturnsFalse()
    {
        var result = await _repository.ExistsAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task GetByDeckIdAsync_ExistingDeck_ReturnsCards()
    {
        var deck1 = new Deck { Name = "Deck 1" };
        var deck2 = new Deck { Name = "Deck 2" };
        _context.Decks.AddRange(deck1, deck2);
        await _context.SaveChangesAsync();

        var card1 = new Card { Name = "Card 1", Suit = "Suit", DeckId = deck1.Id };
        var card2 = new Card { Name = "Card 2", Suit = "Suit", DeckId = deck1.Id };
        var card3 = new Card { Name = "Card 3", Suit = "Suit", DeckId = deck2.Id };
        _context.Cards.AddRange(card1, card2, card3);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByDeckIdAsync(deck1.Id);

        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(deck1.Id, c.DeckId));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllCards()
    {
        var card1 = new Card { Name = "Card 1", Suit = "Suit" };
        var card2 = new Card { Name = "Card 2", Suit = "Suit" };
        _context.Cards.AddRange(card1, card2);
        await _context.SaveChangesAsync();

        var result = await _repository.GetAllAsync();

        Assert.True(result.Count >= 2);
        Assert.Contains(result, c => c.Name == "Card 1");
        Assert.Contains(result, c => c.Name == "Card 2");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

