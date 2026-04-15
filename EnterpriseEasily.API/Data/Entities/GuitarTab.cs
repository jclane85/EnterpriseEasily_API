namespace EnterpriseEasily.API.Data.Entities;

public class GuitarTab
{
    public Guid Id { get; set; }
    public Guid SongId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public string TabType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Song Song { get; set; } = null!;
    public User SubmittedByUser { get; set; } = null!;
}
