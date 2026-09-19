using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.Entities;
using test_project.Repositories;
using Xunit;

namespace test_project.Tests;

public class UserRepositoryTests : IDisposable
{
    private readonly TarotDbContext _context;
    private readonly UserRepository _repository;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TarotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TarotDbContext(options);
        _repository = new UserRepository(_context);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingUser_ReturnsUser()
    {
        var user = new User
        {
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash123",
            Role = "User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("testuser", result.Username);
        Assert.Equal("test@example.com", result.Email);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingUser_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUsernameAsync_ExistingUser_ReturnsUser()
    {
        var user = new User
        {
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash123",
            Role = "User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByUsernameAsync("testuser");

        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_NonExistingUser_ReturnsNull()
    {
        var result = await _repository.GetByUsernameAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmailAsync_ExistingUser_ReturnsUser()
    {
        var user = new User
        {
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash123",
            Role = "User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByEmailAsync("test@example.com");

        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistingUser_ReturnsNull()
    {
        var result = await _repository.GetByEmailAsync("nonexistent@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ValidUser_CreatesUser()
    {
        var user = new User
        {
            Username = "newuser",
            Email = "newuser@example.com",
            PasswordHash = "hash123",
            Role = "User"
        };

        var result = await _repository.CreateAsync(user);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("newuser", result.Username);
        var savedUser = await _context.Users.FindAsync(result.Id);
        Assert.NotNull(savedUser);
    }

    [Fact]
    public async Task UpdateAsync_ExistingUser_UpdatesUser()
    {
        var user = new User
        {
            Username = "originaluser",
            Email = "original@example.com",
            PasswordHash = "hash123",
            Role = "User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        user.Role = "Admin";
        user.Email = "updated@example.com";

        var result = await _repository.UpdateAsync(user);

        Assert.Equal("Admin", result.Role);
        Assert.Equal("updated@example.com", result.Email);
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("Admin", updatedUser.Role);
    }

    [Fact]
    public async Task ExistsAsync_ExistingUser_ReturnsTrue()
    {
        var user = new User
        {
            Username = "existinguser",
            Email = "existing@example.com",
            PasswordHash = "hash123",
            Role = "User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _repository.ExistsAsync(user.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingUser_ReturnsFalse()
    {
        var result = await _repository.ExistsAsync(999);

        Assert.False(result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

