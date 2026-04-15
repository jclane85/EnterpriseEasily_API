using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EnterpriseEasily.API.Data;
using EnterpriseEasily.API.Data.Entities;
using EnterpriseEasily.API.Models;

namespace EnterpriseEasily.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FavoritesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetFavorites()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var favorites = await _db.UserFavorites
            .Where(uf => uf.UserId == user.Id)
            .Include(uf => uf.Song)
                .ThenInclude(s => s.Artist)
            .OrderByDescending(uf => uf.CreatedAt)
            .Select(uf => new SongDto
            {
                Id = uf.Song.Id,
                Title = uf.Song.Title,
                ArtistName = uf.Song.Artist.Name,
                MusicBrainzRecordingId = uf.Song.MusicBrainzRecordingId,
                TabCount = _db.GuitarTabs.Count(t => t.SongId == uf.Song.Id && t.Status == "Approved")
            })
            .ToListAsync();

        return Ok(favorites);
    }

    [HttpGet("ids")]
    public async Task<IActionResult> GetFavoriteIds()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var ids = await _db.UserFavorites
            .Where(uf => uf.UserId == user.Id)
            .Select(uf => uf.SongId)
            .ToListAsync();

        return Ok(ids);
    }

    [HttpPost("{songId}")]
    public async Task<IActionResult> AddFavorite(Guid songId)
    {
        var song = await _db.Songs.FindAsync(songId);
        if (song == null) return NotFound(new { message = "Song not found." });

        var user = await GetOrCreateUserAsync();

        var exists = await _db.UserFavorites
            .AnyAsync(uf => uf.UserId == user.Id && uf.SongId == songId);

        if (exists) return Ok(new { message = "Already favorited." });

        _db.UserFavorites.Add(new UserFavorite
        {
            UserId = user.Id,
            SongId = songId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Created($"/api/favorites/{songId}", new { message = "Added to favorites." });
    }

    [HttpDelete("{songId}")]
    public async Task<IActionResult> RemoveFavorite(Guid songId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var fav = await _db.UserFavorites
            .FirstOrDefaultAsync(uf => uf.UserId == user.Id && uf.SongId == songId);

        if (fav == null) return NotFound(new { message = "Not in favorites." });

        _db.UserFavorites.Remove(fav);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Removed from favorites." });
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var auth0Sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(auth0Sub)) return null;
        return await _db.Users.FirstOrDefaultAsync(u => u.Auth0Sub == auth0Sub);
    }

    private async Task<User> GetOrCreateUserAsync()
    {
        var auth0Sub = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Auth0Sub == auth0Sub);
        if (user != null) return user;

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? "";
        var name = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name) ?? email;

        user = new User
        {
            Id = Guid.NewGuid(),
            Auth0Sub = auth0Sub,
            Email = email,
            DisplayName = name,
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}
