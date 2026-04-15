namespace EnterpriseEasily.API.Data.Entities;

public class Song
{
    public Guid Id { get; set; }
    public string MusicBrainzRecordingId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid ArtistId { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;

    public Artist Artist { get; set; } = null!;
    public ICollection<GuitarTab> GuitarTabs { get; set; } = new List<GuitarTab>();
}
