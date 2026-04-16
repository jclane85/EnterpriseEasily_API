using Microsoft.AspNetCore.Mvc;
using EnterpriseEasily.API.Controllers;
using EnterpriseEasily.API.Data;
using EnterpriseEasily.API.Data.Entities;
using EnterpriseEasily.API.Models;
using EnterpriseEasily.API.Tests.Helpers;

namespace EnterpriseEasily.API.Tests.Controllers;

[TestFixture]
public class FavoritesControllerTests
{
    private AppDbContext _db = null!;
    private FavoritesController _controller = null!;
    private User _user = null!;
    private Artist _artist = null!;
    private Song _song = null!;

    [SetUp]
    public void Setup()
    {
        _db = TestHelper.CreateInMemoryDb();

        _user = TestHelper.CreateTestUser();
        _db.Users.Add(_user);

        _artist = TestHelper.CreateTestArtist("The Smashing Pumpkins");
        _db.Artists.Add(_artist);

        _song = TestHelper.CreateTestSong(_artist.Id, "Today");
        _db.Songs.Add(_song);

        _db.SaveChanges();

        _controller = new FavoritesController(_db);
        TestHelper.SetUser(_controller, TestHelper.CreateUserPrincipal(_user.Auth0Sub));
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    // --- GetFavorites ---

    [Test]
    public async Task GetFavorites_ReturnsEmptyList_WhenNoFavorites()
    {
        var result = await _controller.GetFavorites();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var favorites = ok!.Value as List<SongDto>;
        Assert.That(favorites, Is.Not.Null);
        Assert.That(favorites!, Is.Empty);
    }

    [Test]
    public async Task GetFavorites_ReturnsFavoritedSongs()
    {
        _db.UserFavorites.Add(new UserFavorite
        {
            UserId = _user.Id,
            SongId = _song.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetFavorites();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var favorites = ok!.Value as List<SongDto>;
        Assert.That(favorites, Has.Count.EqualTo(1));
        Assert.That(favorites![0].Title, Is.EqualTo("Today"));
        Assert.That(favorites[0].ArtistName, Is.EqualTo("The Smashing Pumpkins"));
    }

    [Test]
    public async Task GetFavorites_ReturnsUnauthorized_WhenUserNotInDb()
    {
        TestHelper.SetUser(_controller, TestHelper.CreateUserPrincipal("auth0|unknown"));

        var result = await _controller.GetFavorites();

        Assert.That(result, Is.InstanceOf<UnauthorizedResult>());
    }

    [Test]
    public async Task GetFavorites_IncludesTabCount()
    {
        var tab = TestHelper.CreateTestTab(_song.Id, _user.Id, "ASCII", "Approved");
        _db.GuitarTabs.Add(tab);
        _db.UserFavorites.Add(new UserFavorite
        {
            UserId = _user.Id,
            SongId = _song.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetFavorites();

        var ok = result as OkObjectResult;
        var favorites = ok!.Value as List<SongDto>;
        Assert.That(favorites![0].TabCount, Is.EqualTo(1));
    }

    // --- GetFavoriteIds ---

    [Test]
    public async Task GetFavoriteIds_ReturnsEmptyList_WhenNoFavorites()
    {
        var result = await _controller.GetFavoriteIds();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var ids = ok!.Value as List<Guid>;
        Assert.That(ids, Is.Empty);
    }

    [Test]
    public async Task GetFavoriteIds_ReturnsSongIds()
    {
        _db.UserFavorites.Add(new UserFavorite
        {
            UserId = _user.Id,
            SongId = _song.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetFavoriteIds();

        var ok = result as OkObjectResult;
        var ids = ok!.Value as List<Guid>;
        Assert.That(ids, Has.Count.EqualTo(1));
        Assert.That(ids![0], Is.EqualTo(_song.Id));
    }

    [Test]
    public async Task GetFavoriteIds_ReturnsUnauthorized_WhenUserNotInDb()
    {
        TestHelper.SetUser(_controller, TestHelper.CreateUserPrincipal("auth0|unknown"));

        var result = await _controller.GetFavoriteIds();

        Assert.That(result, Is.InstanceOf<UnauthorizedResult>());
    }

    // --- AddFavorite ---

    [Test]
    public async Task AddFavorite_CreatesNewFavorite()
    {
        var result = await _controller.AddFavorite(_song.Id);

        Assert.That(result, Is.InstanceOf<CreatedResult>());
        var count = _db.UserFavorites.Count(uf => uf.UserId == _user.Id && uf.SongId == _song.Id);
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task AddFavorite_ReturnsOk_WhenAlreadyFavorited()
    {
        _db.UserFavorites.Add(new UserFavorite
        {
            UserId = _user.Id,
            SongId = _song.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.AddFavorite(_song.Id);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var count = _db.UserFavorites.Count(uf => uf.UserId == _user.Id && uf.SongId == _song.Id);
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task AddFavorite_ReturnsNotFound_WhenSongDoesNotExist()
    {
        var result = await _controller.AddFavorite(Guid.NewGuid());

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task AddFavorite_CreatesUser_WhenUserNotInDb()
    {
        TestHelper.SetUser(_controller, TestHelper.CreateUserPrincipal("auth0|newuser", "new@test.com", "New User"));

        var result = await _controller.AddFavorite(_song.Id);

        Assert.That(result, Is.InstanceOf<CreatedResult>());
        var newUser = _db.Users.FirstOrDefault(u => u.Auth0Sub == "auth0|newuser");
        Assert.That(newUser, Is.Not.Null);
        Assert.That(newUser!.Email, Is.EqualTo("new@test.com"));
    }

    // --- RemoveFavorite ---

    [Test]
    public async Task RemoveFavorite_RemovesFavorite()
    {
        _db.UserFavorites.Add(new UserFavorite
        {
            UserId = _user.Id,
            SongId = _song.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.RemoveFavorite(_song.Id);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var count = _db.UserFavorites.Count(uf => uf.UserId == _user.Id && uf.SongId == _song.Id);
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task RemoveFavorite_ReturnsNotFound_WhenNotFavorited()
    {
        var result = await _controller.RemoveFavorite(_song.Id);

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task RemoveFavorite_ReturnsUnauthorized_WhenUserNotInDb()
    {
        TestHelper.SetUser(_controller, TestHelper.CreateUserPrincipal("auth0|unknown"));

        var result = await _controller.RemoveFavorite(_song.Id);

        Assert.That(result, Is.InstanceOf<UnauthorizedResult>());
    }
}
