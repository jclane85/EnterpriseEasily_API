namespace EnterpriseEasily.API.Data.Entities;

public class Artist
{
    public Guid Id { get; set; }
    public string MusicBrainzId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Song> Songs { get; set; } = new List<Song>();
}
