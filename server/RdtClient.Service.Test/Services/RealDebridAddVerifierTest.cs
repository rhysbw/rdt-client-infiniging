using Microsoft.Extensions.Logging;
using Moq;
using RdtClient.Data.Models.Internal;
using RdtClient.Data.Models.TorrentClient;
using RdtClient.Service.Exceptions;
using RdtClient.Service.Services;
using RdtClient.Service.Services.TorrentClients;
using Xunit;

namespace RdtClient.Service.Test.Services;

public class RealDebridAddVerifierTest
{
    private readonly Mock<ILogger<RealDebridAddVerifier>> _loggerMock;
    private readonly Mock<RealDebridTorrentClient> _realDebridClientMock;
    private readonly RealDebridAddVerifier _verifier;

    public RealDebridAddVerifierTest()
    {
        _loggerMock = new Mock<ILogger<RealDebridAddVerifier>>();
        _realDebridClientMock = new Mock<RealDebridTorrentClient>();
        _verifier = new RealDebridAddVerifier(_loggerMock.Object, _realDebridClientMock.Object);
    }

    [Fact]
    public async Task VerifyInitialStatusOrThrowAsync_WhenInfringing_ShouldThrowException()
    {
        // Arrange
        var torrentId = "test-torrent-id";
        var torrentInfo = new TorrentClientTorrent { Status = "infringing" };
        
        _realDebridClientMock.Setup(x => x.GetTorrentInfoAsync(torrentId, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(torrentInfo);
        
        _realDebridClientMock.Setup(x => x.DeleteAsync(torrentId, It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RealDebridFailFastException>(
            () => _verifier.VerifyInitialStatusOrThrowAsync(torrentId));
        
        Assert.Equal("infringing", exception.Status);
        Assert.Contains("Real-Debrid rejected this torrent as infringing", exception.Message);
        
        // Verify cleanup was attempted
        _realDebridClientMock.Verify(x => x.DeleteAsync(torrentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyInitialStatusOrThrowAsync_WhenVirus_ShouldThrowException()
    {
        // Arrange
        var torrentId = "test-torrent-id";
        var torrentInfo = new TorrentClientTorrent { Status = "virus" };
        
        _realDebridClientMock.Setup(x => x.GetTorrentInfoAsync(torrentId, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(torrentInfo);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RealDebridFailFastException>(
            () => _verifier.VerifyInitialStatusOrThrowAsync(torrentId));
        
        Assert.Equal("virus", exception.Status);
    }

    [Fact]
    public async Task VerifyInitialStatusOrThrowAsync_WhenDownloaded_ShouldNotThrow()
    {
        // Arrange
        var torrentId = "test-torrent-id";
        var torrentInfo = new TorrentClientTorrent { Status = "downloaded" };
        
        _realDebridClientMock.Setup(x => x.GetTorrentInfoAsync(torrentId, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(torrentInfo);

        // Act & Assert
        await _verifier.VerifyInitialStatusOrThrowAsync(torrentId);
        
        // Verify no cleanup was attempted
        _realDebridClientMock.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyInitialStatusOrThrowAsync_WhenQueued_ShouldNotThrow()
    {
        // Arrange
        var torrentId = "test-torrent-id";
        var torrentInfo = new TorrentClientTorrent { Status = "queued" };
        
        _realDebridClientMock.Setup(x => x.GetTorrentInfoAsync(torrentId, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(torrentInfo);

        // Act & Assert
        await _verifier.VerifyInitialStatusOrThrowAsync(torrentId);
    }

    [Fact]
    public async Task VerifyInitialStatusOrThrowAsync_WhenTimeout_ShouldNotThrow()
    {
        // Arrange
        var torrentId = "test-torrent-id";
        var torrentInfo = new TorrentClientTorrent { Status = "processing" }; // Neither fatal nor clearly OK
        
        _realDebridClientMock.Setup(x => x.GetTorrentInfoAsync(torrentId, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(torrentInfo);

        // Act & Assert - should not throw due to timeout
        await _verifier.VerifyInitialStatusOrThrowAsync(torrentId);
    }

    [Fact]
    public async Task VerifyInitialStatusOrThrowAsync_WhenGetTorrentInfoThrows_ShouldNotThrow()
    {
        // Arrange
        var torrentId = "test-torrent-id";
        
        _realDebridClientMock.Setup(x => x.GetTorrentInfoAsync(torrentId, It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new Exception("API error"));

        // Act & Assert - should not throw, should handle gracefully
        await _verifier.VerifyInitialStatusOrThrowAsync(torrentId);
    }
}