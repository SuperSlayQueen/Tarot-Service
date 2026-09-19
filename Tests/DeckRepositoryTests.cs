using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.Entities;
using test_project.Repositories;
using Xunit;

namespace test_project.Tests;

public class DeckRepositoryTests : IDisposable
{
    private readonly TarotDbContext _context;
    private readonly DeckRepository _repository;

    public DeckRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TarotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TarotDbContext(options);
        _repository = new DeckRepository(_context);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingDeck_ReturnsDeck()
    {
        var deck = new Deck
        {
            Name = "Test Deck",
            Description = "Test Description",
            Author = "Test Author"
        };
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(deck.Id);

        Assert.NotNull(result);
        Assert.Equal(deck.Id, result.Id);
        Assert.Equal("Test Deck", result.Name);
        Assert.Equal("Test Description", result.Description);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingDeck_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ValidDeck_CreatesDeck()
    {
        var deck = new Deck
        {
            Name = "New Deck",
            Description = "Description",
            Author = "Author"
        };

        var result = await _repository.CreateAsync(deck);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("New Deck", result.Name);
        var savedDeck = await _context.Decks.FindAsync(result.Id);
        Assert.NotNull(savedDeck);
    }

    [Fact]
    public async Task UpdateAsync_ExistingDeck_UpdatesDeck()
    {
        var deck = new Deck
        {
            Name = "Original Name",
            Description = "Original Description",
            Author = "Original Author"
        };
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        deck.Name = "Updated Name";
        deck.Description = "Updated Description";

        var result = await _repository.UpdateAsync(deck);

        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Updated Description", result.Description);
        var updatedDeck = await _context.Decks.FindAsync(deck.Id);
        Assert.NotNull(updatedDeck);
        Assert.Equal("Updated Name", updatedDeck.Name);
    }

    [Fact]
    public async Task DeleteAsync_ExistingDeck_DeletesDeck()
    {
        var deck = new Deck { Name = "Deck to Delete" };
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();
        var deckId = deck.Id;

        await _repository.DeleteAsync(deckId);

        var deletedDeck = await _context.Decks.FindAsync(deckId);
        Assert.Null(deletedDeck);
    }

    [Fact]
    public async Task ExistsAsync_ExistingDeck_ReturnsTrue()
    {
        var deck = new Deck { Name = "Existing Deck" };
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        var result = await _repository.ExistsAsync(deck.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingDeck_ReturnsFalse()
    {
        var result = await _repository.ExistsAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllDecks()
    {
        var deck1 = new Deck { Name = "Deck 1" };
        var deck2 = new Deck { Name = "Deck 2" };
        _context.Decks.AddRange(deck1, deck2);
        await _context.SaveChangesAsync();

        var result = await _repository.GetAllAsync();

        Assert.True(result.Count >= 2);
        Assert.Contains(result, d => d.Name == "Deck 1");
        Assert.Contains(result, d => d.Name == "Deck 2");
    }

    [Fact]
    public async Task GetByIdAsync_IncludesCards()
    {
        var deck = new Deck { Name = "Deck with Cards" };
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        var card1 = new Card { Name = "Card 1", Suit = "Suit", DeckId = deck.Id };
        var card2 = new Card { Name = "Card 2", Suit = "Suit", DeckId = deck.Id };
        _context.Cards.AddRange(card1, card2);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(deck.Id);

        Assert.NotNull(result);
        Assert.NotNull(result.Cards);
        Assert.Equal(2, result.Cards.Count);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

