using Microsoft.AspNetCore.Mvc;
using EnterpriseEasily.API.Controllers;
using EnterpriseEasily.API.Data;
using EnterpriseEasily.API.Data.Entities;
using EnterpriseEasily.API.Models;
using EnterpriseEasily.API.Tests.Helpers;

namespace EnterpriseEasily.API.Tests.Controllers;

[TestFixture]
public class TabsControllerTests
{
    private AppDbContext _db = null!;
    private TabsController _controller = null!;
    private User _user = null!;
    private Artist _artist = null!;
    private Song _song = null!;

    [SetUp]
    public void Setup()
    {
        _db = TestHelper.CreateInMemoryDb();

        _user = TestHelper.CreateTestUser();
        _db.Users.Add(_user);

        _artist = TestHelper.CreateTestArtist("Nirvana");
        _db.Artists.Add(_artist);

        _song = TestHelper.CreateTestSong(_artist.Id, "Smells Like Teen Spirit");
        _db.Songs.Add(_song);

        _db.SaveChanges();

        _controller = new TabsController(_db);
        TestHelper.SetUser(_controller, TestHelper.CreateUserPrincipal(_user.Auth0Sub));
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    // --- GetTabs ---

    [Test]
    public async Task GetTabs_ReturnsNotFound_WhenSongDoesNotExist()
    {
        var result = await _controller.GetTabs(Guid.NewGuid());

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task GetTabs_ReturnsEmptyList_WhenNoTabs()
    {
        var result = await _controller.GetTabs(_song.Id);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var tabs = ok!.Value as List<GuitarTabDto>;
        Assert.That(tabs, Is.Empty);
    }

    [Test]
    public async Task GetTabs_ReturnsApprovedTabs()
    {
        _db.GuitarTabs.Add(TestHelper.CreateTestTab(_song.Id, _user.Id, "ASCII", "Approved"));
        _db.GuitarTabs.Add(TestHelper.CreateTestTab(_song.Id, _user.Id, "ChordChart", "Approved"));
        await _db.SaveChangesAsync();

        var result = await _controller.GetTabs(_song.Id);

        var ok = result as OkObjectResult;
        var tabs = ok!.Value as List<GuitarTabDto>;
        Assert.That(tabs, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetTabs_IncludesUsersPendingTabs()
    {
        _db.GuitarTabs.Add(TestHelper.CreateTestTab(_song.Id, _user.Id, "ASCII", "Pending"));
        await _db.SaveChangesAsync();

        var result = await _controller.GetTabs(_song.Id);

        var ok = result as OkObjectResult;
        var tabs = ok!.Value as List<GuitarTabDto>;
        Assert.That(tabs, Has.Count.EqualTo(1));
        Assert.That(tabs![0].Status, Is.EqualTo("Pending"));
    }

    [Test]
    public async Task GetTabs_ExcludesOtherUsersPendingTabs()
    {
        var otherUser = TestHelper.CreateTestUser("auth0|other", "OtherUser");
        _db.Users.Add(otherUser);
        _db.GuitarTabs.Add(TestHelper.CreateTestTab(_song.Id, otherUser.Id, "ASCII", "Pending"));
        await _db.SaveChangesAsync();

        var result = await _controller.GetTabs(_song.Id);

        var ok = result as OkObjectResult;
        var tabs = ok!.Value as List<GuitarTabDto>;
        Assert.That(tabs, Is.Empty);
    }

    // --- SubmitTab ---

    [Test]
    public async Task SubmitTab_CreatesTabWithPendingStatus()
    {
        var request = new SubmitTabRequest { TabType = "ASCII", Content = "e|---0---|" };

        var result = await _controller.SubmitTab(_song.Id, request);

        Assert.That(result, Is.InstanceOf<CreatedResult>());
        var tab = _db.GuitarTabs.FirstOrDefault(t => t.SongId == _song.Id);
        Assert.That(tab, Is.Not.Null);
        Assert.That(tab!.Status, Is.EqualTo("Pending"));
        Assert.That(tab.Content, Is.EqualTo("e|---0---|"));
    }

    [Test]
    public async Task SubmitTab_ReturnsCreatedDto()
    {
        var request = new SubmitTabRequest { TabType = "ChordChart", Content = "[Verse]\nAm C G" };

        var result = await _controller.SubmitTab(_song.Id, request);

        var created = result as CreatedResult;
        Assert.That(created, Is.Not.Null);
        var dto = created!.Value as GuitarTabDto;
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.TabType, Is.EqualTo("ChordChart"));
        Assert.That(dto.Status, Is.EqualTo("Pending"));
        Assert.That(dto.SubmittedBy, Is.EqualTo("TestUser"));
    }

    [Test]
    public async Task SubmitTab_ReturnsBadRequest_WhenContentEmpty()
    {
        var request = new SubmitTabRequest { TabType = "ASCII", Content = "  " };

        var result = await _controller.SubmitTab(_song.Id, request);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task SubmitTab_ReturnsBadRequest_WhenTabTypeInvalid()
    {
        var request = new SubmitTabRequest { TabType = "PDF", Content = "some content" };

        var result = await _controller.SubmitTab(_song.Id, request);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task SubmitTab_ReturnsNotFound_WhenSongDoesNotExist()
    {
        var request = new SubmitTabRequest { TabType = "ASCII", Content = "e|---0---|" };

        var result = await _controller.SubmitTab(Guid.NewGuid(), request);

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task SubmitTab_CreatesUser_WhenUserNotInDb()
    {
        TestHelper.SetUser(_controller, TestHelper.CreateUserPrincipal("auth0|newguy", "newguy@test.com", "NewGuy"));
        var request = new SubmitTabRequest { TabType = "ASCII", Content = "e|---0---|" };

        var result = await _controller.SubmitTab(_song.Id, request);

        Assert.That(result, Is.InstanceOf<CreatedResult>());
        var newUser = _db.Users.FirstOrDefault(u => u.Auth0Sub == "auth0|newguy");
        Assert.That(newUser, Is.Not.Null);
        Assert.That(newUser!.DisplayName, Is.EqualTo("NewGuy"));
    }

    [Test]
    public async Task SubmitTab_AssignsCorrectSongId()
    {
        var request = new SubmitTabRequest { TabType = "ASCII", Content = "e|---0---|" };

        await _controller.SubmitTab(_song.Id, request);

        var tab = _db.GuitarTabs.First();
        Assert.That(tab.SongId, Is.EqualTo(_song.Id));
        Assert.That(tab.SubmittedByUserId, Is.EqualTo(_user.Id));
    }
}
