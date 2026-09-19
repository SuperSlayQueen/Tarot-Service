using Microsoft.EntityFrameworkCore;
using test_project.Models.Entities;

namespace test_project.Data;

public class TarotDbContext : DbContext
{
    public TarotDbContext(DbContextOptions<TarotDbContext> options) : base(options)
    {
    }

    // Only derived contexts pass their own typed options through this constructor.
    protected TarotDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Card> Cards { get; set; }
    public DbSet<Deck> Decks { get; set; }
    public DbSet<Spread> Spreads { get; set; }
    public DbSet<SpreadCard> SpreadCards { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<Reading> Readings { get; set; }
    public DbSet<ReadingCard> ReadingCards { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Card>(entity =>
        {
            entity.ToTable("cards");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200).HasColumnName("name");
            entity.Property(e => e.Description).HasMaxLength(2000).HasColumnName("description");
            entity.Property(e => e.ImageUrl).HasMaxLength(500).HasColumnName("image_url");
            entity.Property(e => e.Suit).HasMaxLength(100).HasColumnName("suit");
            entity.Property(e => e.Number).HasColumnName("number");
            entity.Property(e => e.UprightMeaning).HasMaxLength(1000).HasColumnName("upright_meaning");
            entity.Property(e => e.ReversedMeaning).HasMaxLength(1000).HasColumnName("reversed_meaning");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.DeckId).HasColumnName("deck_id");
            
            entity.HasOne(e => e.Deck)
                  .WithMany(d => d.Cards)
                  .HasForeignKey(e => e.DeckId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Deck>(entity =>
        {
            entity.ToTable("decks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200).HasColumnName("name");
            entity.Property(e => e.Description).HasMaxLength(2000).HasColumnName("description");
            entity.Property(e => e.Author).HasMaxLength(200).HasColumnName("author");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Spread>(entity =>
        {
            entity.ToTable("spreads");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200).HasColumnName("name");
            entity.Property(e => e.Description).HasMaxLength(2000).HasColumnName("description");
            entity.Property(e => e.NumberOfPositions).HasColumnName("number_of_positions");
            entity.Property(e => e.PositionNames).HasMaxLength(500).HasColumnName("position_names");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<SpreadCard>(entity =>
        {
            entity.ToTable("spread_cards");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SpreadId).HasColumnName("spread_id");
            entity.Property(e => e.CardId).HasColumnName("card_id");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.IsReversed).HasColumnName("is_reversed");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            
            entity.HasOne(e => e.Spread)
                  .WithMany(s => s.SpreadCards)
                  .HasForeignKey(e => e.SpreadId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Card)
                  .WithMany(c => c.SpreadCards)
                  .HasForeignKey(e => e.CardId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.SpreadId, e.Position })
                  .IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100).HasColumnName("username");
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200).HasColumnName("email");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500).HasColumnName("password_hash");
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50).HasColumnName("role");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Key).IsRequired().HasMaxLength(500).HasColumnName("key");
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200).HasColumnName("name");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        });

        modelBuilder.Entity<Reading>(entity =>
        {
            entity.ToTable("readings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SpreadId).HasColumnName("spread_id");
            entity.Property(e => e.Question).HasMaxLength(1000).HasColumnName("question");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Spread)
                .WithMany()
                .HasForeignKey(e => e.SpreadId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
        });

        modelBuilder.Entity<ReadingCard>(entity =>
        {
            entity.ToTable("reading_cards");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ReadingId).HasColumnName("reading_id");
            entity.Property(e => e.CardId).HasColumnName("card_id");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.IsReversed).HasColumnName("is_reversed");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Reading)
                .WithMany(r => r.ReadingCards)
                .HasForeignKey(e => e.ReadingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Card)
                .WithMany()
                .HasForeignKey(e => e.CardId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ReadingId, e.Position }).IsUnique();
        });
    }
}