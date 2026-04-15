namespace EnterpriseEasily.API.Data.Entities;

public class UserFavorite
{
    public Guid UserId { get; set; }
    public Guid SongId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Song Song { get; set; } = null!;
}
