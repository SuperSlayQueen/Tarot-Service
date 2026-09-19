using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.Entities;
using test_project.Repositories;
using Xunit;

namespace test_project.Tests;

public class SpreadRepositoryTests : IDisposable
{
    private readonly TarotDbContext _context;
    private readonly SpreadRepository _repository;

    public SpreadRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TarotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TarotDbContext(options);
        _repository = new SpreadRepository(_context);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingSpread_ReturnsSpread()
    {
        var spread = new Spread
        {
            Name = "Test Spread",
            Description = "Test Description",
            NumberOfPositions = 3
        };
        _context.Spreads.Add(spread);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(spread.Id);

        Assert.NotNull(result);
        Assert.Equal(spread.Id, result.Id);
        Assert.Equal("Test Spread", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingSpread_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ValidSpread_CreatesSpread()
    {
        var spread = new Spread
        {
            Name = "New Spread",
            Description = "Description",
            NumberOfPositions = 5
        };

        var result = await _repository.CreateAsync(spread);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("New Spread", result.Name);
        var savedSpread = await _context.Spreads.FindAsync(result.Id);
        Assert.NotNull(savedSpread);
    }

    [Fact]
    public async Task UpdateAsync_ExistingSpread_UpdatesSpread()
    {
        var spread = new Spread
        {
            Name = "Original Name",
            Description = "Original Description",
            NumberOfPositions = 3
        };
        _context.Spreads.Add(spread);
        await _context.SaveChangesAsync();

        spread.Name = "Updated Name";
        spread.Description = "Updated Description";

        var result = await _repository.UpdateAsync(spread);

        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Updated Description", result.Description);
        var updatedSpread = await _context.Spreads.FindAsync(spread.Id);
        Assert.NotNull(updatedSpread);
        Assert.Equal("Updated Name", updatedSpread.Name);
    }

    [Fact]
    public async Task DeleteAsync_ExistingSpread_DeletesSpread()
    {
        var spread = new Spread
        {
            Name = "Spread to Delete",
            Description = "Description",
            NumberOfPositions = 3
        };
        _context.Spreads.Add(spread);
        await _context.SaveChangesAsync();
        var spreadId = spread.Id;

        await _repository.DeleteAsync(spreadId);

        var deletedSpread = await _context.Spreads.FindAsync(spreadId);
        Assert.Null(deletedSpread);
    }

    [Fact]
    public async Task ExistsAsync_ExistingSpread_ReturnsTrue()
    {
        var spread = new Spread
        {
            Name = "Existing Spread",
            Description = "Description",
            NumberOfPositions = 3
        };
        _context.Spreads.Add(spread);
        await _context.SaveChangesAsync();

        var result = await _repository.ExistsAsync(spread.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingSpread_ReturnsFalse()
    {
        var result = await _repository.ExistsAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSpreads()
    {
        var spread1 = new Spread { Name = "Spread 1", Description = "Desc", NumberOfPositions = 3 };
        var spread2 = new Spread { Name = "Spread 2", Description = "Desc", NumberOfPositions = 5 };
        _context.Spreads.AddRange(spread1, spread2);
        await _context.SaveChangesAsync();

        var result = await _repository.GetAllAsync();

        Assert.True(result.Count >= 2);
        Assert.Contains(result, s => s.Name == "Spread 1");
        Assert.Contains(result, s => s.Name == "Spread 2");
    }

    [Fact]
    public async Task AddCardToSpreadAsync_ValidData_CreatesSpreadCard()
    {
        var spread = new Spread { Name = "Test Spread", Description = "Desc", NumberOfPositions = 3 };
        var card = new Card { Name = "Test Card", Suit = "Suit" };
        _context.Spreads.Add(spread);
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        var spreadCard = new SpreadCard
        {
            SpreadId = spread.Id,
            CardId = card.Id,
            Position = 1,
            IsReversed = false
        };

        var result = await _repository.AddCardToSpreadAsync(spreadCard);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        var savedSpreadCard = await _context.SpreadCards.FindAsync(result.Id);
        Assert.NotNull(savedSpreadCard);
    }

    [Fact]
    public async Task RemoveCardFromSpreadAsync_ExistingSpreadCard_DeletesSpreadCard()
    {
        var spread = new Spread { Name = "Test Spread", Description = "Desc", NumberOfPositions = 3 };
        var card = new Card { Name = "Test Card", Suit = "Suit" };
        _context.Spreads.Add(spread);
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        var spreadCard = new SpreadCard
        {
            SpreadId = spread.Id,
            CardId = card.Id,
            Position = 1,
            IsReversed = false
        };
        _context.SpreadCards.Add(spreadCard);
        await _context.SaveChangesAsync();
        var spreadCardId = spreadCard.Id;

        await _repository.RemoveCardFromSpreadAsync(spreadCardId);

        var deletedSpreadCard = await _context.SpreadCards.FindAsync(spreadCardId);
        Assert.Null(deletedSpreadCard);
    }

    [Fact]
    public async Task GetSpreadCardByIdAsync_ExistingSpreadCard_ReturnsSpreadCard()
    {
        var spread = new Spread { Name = "Test Spread", Description = "Desc", NumberOfPositions = 3 };
        var card = new Card { Name = "Test Card", Suit = "Suit" };
        _context.Spreads.Add(spread);
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        var spreadCard = new SpreadCard
        {
            SpreadId = spread.Id,
            CardId = card.Id,
            Position = 1,
            IsReversed = false
        };
        _context.SpreadCards.Add(spreadCard);
        await _context.SaveChangesAsync();

        var result = await _repository.GetSpreadCardByIdAsync(spreadCard.Id);

        Assert.NotNull(result);
        Assert.Equal(spreadCard.Id, result.Id);
        Assert.Equal(spread.Id, result.SpreadId);
        Assert.Equal(card.Id, result.CardId);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

