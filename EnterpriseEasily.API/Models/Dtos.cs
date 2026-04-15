namespace EnterpriseEasily.API.Models;

public class SearchResultDto
{
    public List<SongDto> Songs { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class SongDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string MusicBrainzRecordingId { get; set; } = string.Empty;
    public int TabCount { get; set; }
}

public class GuitarTabDto
{
    public Guid Id { get; set; }
    public string TabType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SubmitTabRequest
{
    public string TabType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
