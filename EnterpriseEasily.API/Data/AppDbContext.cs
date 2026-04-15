using Microsoft.EntityFrameworkCore;
using EnterpriseEasily.API.Data.Entities;

namespace EnterpriseEasily.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<GuitarTab> GuitarTabs => Set<GuitarTab>();
    public DbSet<UserFavorite> UserFavorites => Set<UserFavorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Auth0Sub).IsUnique();
            e.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("User");
        });

        modelBuilder.Entity<Artist>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.MusicBrainzId).IsUnique();
        });

        modelBuilder.Entity<Song>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.MusicBrainzRecordingId).IsUnique();
            e.HasIndex(s => s.ArtistId);
            e.HasOne(s => s.Artist).WithMany(a => a.Songs).HasForeignKey(s => s.ArtistId);
        });

        modelBuilder.Entity<GuitarTab>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.SongId, t.Status });
            e.Property(t => t.Status).HasMaxLength(20).HasDefaultValue("Pending");
            e.Property(t => t.TabType).HasMaxLength(20);
            e.HasOne(t => t.Song).WithMany(s => s.GuitarTabs).HasForeignKey(t => t.SongId);
            e.HasOne(t => t.SubmittedByUser).WithMany().HasForeignKey(t => t.SubmittedByUserId);
        });

        modelBuilder.Entity<UserFavorite>(e =>
        {
            e.HasKey(uf => new { uf.UserId, uf.SongId });
            e.HasOne(uf => uf.User).WithMany(u => u.Favorites).HasForeignKey(uf => uf.UserId);
            e.HasOne(uf => uf.Song).WithMany().HasForeignKey(uf => uf.SongId);
        });
    }
}
