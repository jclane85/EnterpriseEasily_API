using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EnterpriseEasily.API.Data;
using EnterpriseEasily.API.Data.Entities;
using EnterpriseEasily.API.Models;

namespace EnterpriseEasily.API.Services;

public class MusicBrainzService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _db;
    private readonly ILogger<MusicBrainzService> _logger;
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    public MusicBrainzService(IHttpClientFactory httpClientFactory, AppDbContext db, ILogger<MusicBrainzService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<SearchResultDto> SearchAsync(string query, int page = 1, int pageSize = 20)
    {
        // MusicBrainz uses 0-based offset
        var offset = (page - 1) * pageSize;

        var client = _httpClientFactory.CreateClient("MusicBrainz");

        // Rate limit: 1 request per second
        await _rateLimiter.WaitAsync();
        try
        {
            var elapsed = DateTime.UtcNow - _lastRequestTime;
            if (elapsed < TimeSpan.FromSeconds(1))
            {
                await Task.Delay(TimeSpan.FromSeconds(1) - elapsed);
            }

            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"recording/?query={encodedQuery}&limit={pageSize}&offset={offset}&fmt=json";

            _logger.LogInformation("MusicBrainz request: {Url}", url);

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            _lastRequestTime = DateTime.UtcNow;

            var json = await response.Content.ReadAsStringAsync();
            var mbResponse = JsonSerializer.Deserialize<MusicBrainzRecordingResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (mbResponse?.Recordings == null)
                return new SearchResultDto { Songs = [], TotalCount = 0, Page = page, PageSize = pageSize };

            var songs = new List<SongDto>();

            foreach (var recording in mbResponse.Recordings)
            {
                var artistCredit = recording.ArtistCredit?.FirstOrDefault();
                var artistName = artistCredit?.Artist?.Name ?? artistCredit?.Name ?? "Unknown Artist";
                var artistMbId = artistCredit?.Artist?.Id ?? "";

                // Cache artist & song in DB
                var (dbArtist, dbSong) = await CacheRecordingAsync(recording.Id, recording.Title, artistMbId, artistName);

                // Check if tabs exist
                var tabCount = await _db.GuitarTabs
                    .CountAsync(t => t.SongId == dbSong.Id && t.Status == "Approved");

                songs.Add(new SongDto
                {
                    Id = dbSong.Id,
                    Title = recording.Title,
                    ArtistName = artistName,
                    MusicBrainzRecordingId = recording.Id,
                    TabCount = tabCount
                });
            }

            return new SearchResultDto
            {
                Songs = songs,
                TotalCount = mbResponse.Count,
                Page = page,
                PageSize = pageSize
            };
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private async Task<(Artist artist, Song song)> CacheRecordingAsync(
        string recordingId, string title, string artistMbId, string artistName)
    {
        // Find or create artist
        var artist = await _db.Artists.FirstOrDefaultAsync(a => a.MusicBrainzId == artistMbId);
        if (artist == null)
        {
            artist = new Artist
            {
                Id = Guid.NewGuid(),
                MusicBrainzId = artistMbId,
                Name = artistName,
                CachedAt = DateTime.UtcNow
            };
            _db.Artists.Add(artist);
        }
        else if (DateTime.UtcNow - artist.CachedAt > CacheTtl)
        {
            artist.Name = artistName;
            artist.CachedAt = DateTime.UtcNow;
        }

        // Find or create song
        var song = await _db.Songs.FirstOrDefaultAsync(s => s.MusicBrainzRecordingId == recordingId);
        if (song == null)
        {
            song = new Song
            {
                Id = Guid.NewGuid(),
                MusicBrainzRecordingId = recordingId,
                Title = title,
                ArtistId = artist.Id,
                CachedAt = DateTime.UtcNow
            };
            _db.Songs.Add(song);
        }
        else if (DateTime.UtcNow - song.CachedAt > CacheTtl)
        {
            song.Title = title;
            song.ArtistId = artist.Id;
            song.CachedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return (artist, song);
    }
}

// MusicBrainz API response models
public class MusicBrainzRecordingResponse
{
    public int Count { get; set; }
    public int Offset { get; set; }
    public List<MusicBrainzRecording> Recordings { get; set; } = [];
}

public class MusicBrainzRecording
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCredit>? ArtistCredit { get; set; }
}

public class MusicBrainzArtistCredit
{
    public string Name { get; set; } = string.Empty;
    public MusicBrainzArtist? Artist { get; set; }
}

public class MusicBrainzArtist
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
