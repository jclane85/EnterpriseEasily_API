using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EnterpriseEasily.API.Data;
using EnterpriseEasily.API.Data.Entities;
using EnterpriseEasily.API.Models;

namespace EnterpriseEasily.API.Controllers;

[ApiController]
[Route("api/songs/{songId}/tabs")]
[Authorize]
public class TabsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TabsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetTabs(Guid songId)
    {
        var song = await _db.Songs.FindAsync(songId);
        if (song == null)
            return NotFound(new { message = "Song not found." });

        var auth0Sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Auth0Sub == auth0Sub);

        var query = _db.GuitarTabs
            .Include(t => t.SubmittedByUser)
            .Where(t => t.SongId == songId);

        // Show approved tabs to everyone; also show the current user's pending/rejected tabs
        if (currentUser != null)
        {
            query = query.Where(t => t.Status == "Approved" || t.SubmittedByUserId == currentUser.Id);
        }
        else
        {
            query = query.Where(t => t.Status == "Approved");
        }

        var tabs = await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new GuitarTabDto
            {
                Id = t.Id,
                TabType = t.TabType,
                Content = t.Content,
                Status = t.Status,
                SubmittedBy = t.SubmittedByUser.DisplayName,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return Ok(tabs);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitTab(Guid songId, [FromBody] SubmitTabRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "Tab content is required." });

        if (request.TabType != "ASCII" && request.TabType != "ChordChart")
            return BadRequest(new { message = "TabType must be 'ASCII' or 'ChordChart'." });

        var song = await _db.Songs.FindAsync(songId);
        if (song == null)
            return NotFound(new { message = "Song not found." });

        var auth0Sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await GetOrCreateUserAsync(auth0Sub!);

        var tab = new GuitarTab
        {
            Id = Guid.NewGuid(),
            SongId = songId,
            SubmittedByUserId = user.Id,
            TabType = request.TabType,
            Content = request.Content,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.GuitarTabs.Add(tab);
        await _db.SaveChangesAsync();

        return Created($"/api/songs/{songId}/tabs", new GuitarTabDto
        {
            Id = tab.Id,
            TabType = tab.TabType,
            Content = tab.Content,
            Status = tab.Status,
            SubmittedBy = user.DisplayName,
            CreatedAt = tab.CreatedAt
        });
    }

    private async Task<User> GetOrCreateUserAsync(string auth0Sub)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Auth0Sub == auth0Sub);
        if (user != null) return user;

        // Extract info from JWT claims
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
