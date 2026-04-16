using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using EnterpriseEasily.API.Controllers;
using EnterpriseEasily.API.Models;
using EnterpriseEasily.API.Services;

namespace EnterpriseEasily.API.Tests.Controllers;

[TestFixture]
public class SearchControllerTests
{
    private Mock<MusicBrainzService> _mockService = null!;
    private SearchController _controller = null!;

    [SetUp]
    public void Setup()
    {
        _mockService = new Mock<MusicBrainzService>(
            MockBehavior.Loose,
            Mock.Of<IHttpClientFactory>(),
            (Data.AppDbContext)null!,
            Mock.Of<ILogger<MusicBrainzService>>());
        _controller = new SearchController(_mockService.Object);
    }

    [Test]
    public async Task Search_ReturnsBadRequest_WhenQueryIsNull()
    {
        var result = await _controller.Search(null!, 1);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Search_ReturnsBadRequest_WhenQueryIsEmpty()
    {
        var result = await _controller.Search("", 1);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Search_ReturnsBadRequest_WhenQueryIsSingleChar()
    {
        var result = await _controller.Search("a", 1);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Search_ReturnsBadRequest_WhenQueryIsWhitespace()
    {
        var result = await _controller.Search("   ", 1);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Search_DefaultsPageToOne_WhenPageIsZero()
    {
        _mockService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new SearchResultDto { Songs = [], TotalCount = 0, Page = 1, PageSize = 20 });

        var result = await _controller.Search("today", 0);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        _mockService.Verify(s => s.SearchAsync("today", 1, It.IsAny<int>()), Times.Once);
    }

    [Test]
    public async Task Search_DefaultsPageToOne_WhenPageIsNegative()
    {
        _mockService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new SearchResultDto { Songs = [], TotalCount = 0, Page = 1, PageSize = 20 });

        var result = await _controller.Search("nirvana", -5);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        _mockService.Verify(s => s.SearchAsync("nirvana", 1, It.IsAny<int>()), Times.Once);
    }

    [Test]
    public async Task Search_ReturnsOk_WithValidQuery()
    {
        var expected = new SearchResultDto
        {
            Songs = [new SongDto { Id = Guid.NewGuid(), Title = "Today", ArtistName = "The Smashing Pumpkins", TabCount = 2 }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };
        _mockService
            .Setup(s => s.SearchAsync("today", 1, It.IsAny<int>()))
            .ReturnsAsync(expected);

        var result = await _controller.Search("today", 1);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var dto = ok!.Value as SearchResultDto;
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Songs, Has.Count.EqualTo(1));
        Assert.That(dto.Songs[0].Title, Is.EqualTo("Today"));
    }

    [Test]
    public async Task Search_PassesCorrectPageNumber()
    {
        _mockService
            .Setup(s => s.SearchAsync("test", 3, It.IsAny<int>()))
            .ReturnsAsync(new SearchResultDto { Songs = [], TotalCount = 0, Page = 3, PageSize = 20 });

        await _controller.Search("test", 3);

        _mockService.Verify(s => s.SearchAsync("test", 3, It.IsAny<int>()), Times.Once);
    }
}
