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

    public virtual async Task<SearchResultDto> SearchAsync(string query, int page = 1, int pageSize = 20)
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

            // On page 1, boost local songs that have tabs but weren't in MB results
            if (page == 1)
            {
                var mbSongIds = songs.Select(s => s.Id).ToHashSet();
                var queryLowerDb = query.ToLowerInvariant().Trim();

                var localMatches = await _db.Songs
                    .Include(s => s.Artist)
                    .Where(s => s.Title.ToLower().Contains(queryLowerDb)
                             || s.Artist.Name.ToLower().Contains(queryLowerDb))
                    .Where(s => _db.GuitarTabs.Any(t => t.SongId == s.Id && t.Status == "Approved"))
                    .Take(10)
                    .ToListAsync();

                foreach (var local in localMatches)
                {
                    if (mbSongIds.Contains(local.Id)) continue;

                    var tabCount = await _db.GuitarTabs
                        .CountAsync(t => t.SongId == local.Id && t.Status == "Approved");

                    songs.Add(new SongDto
                    {
                        Id = local.Id,
                        Title = local.Title,
                        ArtistName = local.Artist.Name,
                        MusicBrainzRecordingId = local.MusicBrainzRecordingId,
                        TabCount = tabCount
                    });
                }
            }

            // Sort: best text match first, then by tab count, then by MusicBrainz score
            var queryLower = query.ToLowerInvariant().Trim();
            var mbScores = mbResponse.Recordings.ToDictionary(r => r.Id, r => r.Score);

            songs = songs
                .OrderByDescending(s => ComputeRelevance(s, queryLower))
                .ThenByDescending(s => s.TabCount)
                .ThenByDescending(s => mbScores.GetValueOrDefault(s.MusicBrainzRecordingId, 0))
                .ToList();

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

    private static int ComputeRelevance(SongDto song, string queryLower)
    {
        var title = song.Title.ToLowerInvariant();
        var artist = song.ArtistName.ToLowerInvariant();

        // Exact matches (highest)
        if (title == queryLower) return 100;
        if (artist == queryLower) return 95;

        // Title or artist equals one of the query words
        var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Starts-with matches
        if (title.StartsWith(queryLower)) return 85;
        if (artist.StartsWith(queryLower)) return 80;

        // Contains as a whole phrase
        if (title.Contains(queryLower)) return 70;
        if (artist.Contains(queryLower)) return 65;

        // Any query word matches title or artist start
        if (queryWords.Any(w => title.StartsWith(w))) return 50;
        if (queryWords.Any(w => artist.StartsWith(w))) return 45;

        // Any query word found in title or artist
        if (queryWords.Any(w => title.Contains(w))) return 30;
        if (queryWords.Any(w => artist.Contains(w))) return 25;

        return 0;
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
    public int Score { get; set; }

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
