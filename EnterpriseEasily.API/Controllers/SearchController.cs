using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EnterpriseEasily.API.Services;

namespace EnterpriseEasily.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly MusicBrainzService _musicBrainzService;

    public SearchController(MusicBrainzService musicBrainzService)
    {
        _musicBrainzService = musicBrainzService;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int page = 1)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(new { message = "Query must be at least 2 characters." });

        if (page < 1) page = 1;

        var result = await _musicBrainzService.SearchAsync(q, page);
        return Ok(result);
    }
}
