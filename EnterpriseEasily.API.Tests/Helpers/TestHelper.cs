using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EnterpriseEasily.API.Data;
using EnterpriseEasily.API.Data.Entities;

namespace EnterpriseEasily.API.Tests.Helpers;

public static class TestHelper
{
    public static AppDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static ClaimsPrincipal CreateUserPrincipal(string auth0Sub, string? email = null, string? name = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth0Sub)
        };
        if (email != null) claims.Add(new Claim(ClaimTypes.Email, email));
        if (name != null) claims.Add(new Claim("name", name));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public static void SetUser(ControllerBase controller, ClaimsPrincipal principal)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    public static User CreateTestUser(string auth0Sub = "auth0|test123", string displayName = "TestUser")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Auth0Sub = auth0Sub,
            Email = "test@example.com",
            DisplayName = displayName,
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Artist CreateTestArtist(string name = "Test Artist")
    {
        return new Artist
        {
            Id = Guid.NewGuid(),
            MusicBrainzId = Guid.NewGuid().ToString(),
            Name = name,
            CachedAt = DateTime.UtcNow
        };
    }

    public static Song CreateTestSong(Guid artistId, string title = "Test Song")
    {
        return new Song
        {
            Id = Guid.NewGuid(),
            MusicBrainzRecordingId = Guid.NewGuid().ToString(),
            Title = title,
            ArtistId = artistId,
            CachedAt = DateTime.UtcNow
        };
    }

    public static GuitarTab CreateTestTab(Guid songId, Guid userId, string tabType = "ASCII", string status = "Approved")
    {
        return new GuitarTab
        {
            Id = Guid.NewGuid(),
            SongId = songId,
            SubmittedByUserId = userId,
            TabType = tabType,
            Content = "e|---0---2---3---|",
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
