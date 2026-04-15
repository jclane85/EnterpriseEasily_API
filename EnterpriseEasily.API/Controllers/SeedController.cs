using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EnterpriseEasily.API.Data;
using EnterpriseEasily.API.Data.Entities;

namespace EnterpriseEasily.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly AppDbContext _db;

    public SeedController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> SeedTabs()
    {
        // Get all cached songs
        var songs = await _db.Songs.Include(s => s.Artist).ToListAsync();
        if (songs.Count == 0)
            return Ok(new { message = "No songs cached yet. Search for something first." });

        // Create a demo user if one doesn't exist
        var demoUser = await _db.Users.FirstOrDefaultAsync(u => u.DisplayName == "TabMaster420");
        if (demoUser == null)
        {
            demoUser = new User
            {
                Id = Guid.NewGuid(),
                Auth0Sub = "demo|seed-user",
                Email = "tabmaster@example.com",
                DisplayName = "TabMaster420",
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(demoUser);
        }

        var seeded = 0;
        foreach (var song in songs.Take(10))
        {
            // Skip if tabs already exist for this song
            if (await _db.GuitarTabs.AnyAsync(t => t.SongId == song.Id))
                continue;

            // Add an ASCII tab
            _db.GuitarTabs.Add(new GuitarTab
            {
                Id = Guid.NewGuid(),
                SongId = song.Id,
                SubmittedByUserId = demoUser.Id,
                TabType = "ASCII",
                Content = GenerateAsciiTab(song.Title, song.Artist.Name),
                Status = "Approved",
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
                UpdatedAt = DateTime.UtcNow
            });

            // Add a chord chart for some songs
            if (Random.Shared.Next(2) == 0)
            {
                _db.GuitarTabs.Add(new GuitarTab
                {
                    Id = Guid.NewGuid(),
                    SongId = song.Id,
                    SubmittedByUserId = demoUser.Id,
                    TabType = "ChordChart",
                    Content = GenerateChordChart(song.Title, song.Artist.Name),
                    Status = "Approved",
                    CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
                    UpdatedAt = DateTime.UtcNow
                });
            }

            seeded++;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = $"Seeded tabs for {seeded} songs.", totalSongs = songs.Count });
    }

    private static string GenerateAsciiTab(string title, string artist)
    {
        var chords = new[] { "Am", "C", "G", "Em", "D", "F", "Bm", "E" };
        var picked = Enumerable.Range(0, 4).Select(_ => chords[Random.Shared.Next(chords.Length)]).ToArray();

        return $"""
            {title} - {artist}
            Standard Tuning (EADGBe)

            e|---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---|
            B|---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---|
            G|---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---|
            D|---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---|
            A|---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---|
            E|---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---{Random.Shared.Next(0, 4)}---|

            Repeat x2, then chorus
            """;
    }

    private static string GenerateChordChart(string title, string artist)
    {
        var chords = new[] { "Am", "C", "G", "Em", "D", "F", "Bm", "E", "A", "Dm" };
        string Pick() => chords[Random.Shared.Next(chords.Length)];

        return $"""
            {title} - {artist}

            [Intro]
            {Pick()}  {Pick()}  {Pick()}  {Pick()}

            [Verse 1]
            {Pick()}                    {Pick()}
            Walking down the road again
            {Pick()}                    {Pick()}
            Searching for the words to say
            {Pick()}                    {Pick()}
            Every note I play reminds me
            {Pick()}                    {Pick()}
            Of a brighter, better day

            [Chorus]
            {Pick()}        {Pick()}        {Pick()}
            So sing it loud, sing it clear
            {Pick()}        {Pick()}
            Let the music take you here
            {Pick()}        {Pick()}        {Pick()}
            Through the storm, through the night
            {Pick()}        {Pick()}
            Every chord will feel just right

            [Verse 2]
            {Pick()}                    {Pick()}
            Fingers tracing every string
            {Pick()}                    {Pick()}
            Melodies that make hearts sing
            {Pick()}                    {Pick()}
            In this moment, nothing else
            {Pick()}                    {Pick()}
            Matters but the song it brings

            [Bridge]
            {Pick()}        {Pick()}
            And when the silence falls around
            {Pick()}        {Pick()}
            We'll make the only sound
            {Pick()}
            That ever really mattered

            [Outro]
            {Pick()}  {Pick()}  {Pick()}  {Pick()}
            Every chord will feel just right...
            """;
    }
}
