using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RdtClient.Data.Models.Internal;
using RdtClient.Data.Models.TorrentClient;
using RdtClient.Service.Exceptions;
using RdtClient.Service.Services.TorrentClients;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace RdtClient.Service.Test.Integration;

/// <summary>
/// Integration tests to verify that Sonarr receives the correct HTTP status codes
/// when Real-Debrid reports infringing torrents, ensuring Sonarr tries the next download client.
/// </summary>
public class SonarrFailFastIntegrationTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SonarrFailFastIntegrationTest(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Mock the RealDebridTorrentClient to simulate infringing torrents
                var mockRealDebridClient = new Mock<RealDebridTorrentClient>();
                
                // Setup mock to return infringing status
                mockRealDebridClient.Setup(x => x.GetTorrentInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new TorrentClientTorrent { Status = "infringing" });
                
                mockRealDebridClient.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                  .Returns(Task.CompletedTask);

                services.AddScoped<RealDebridTorrentClient>(_ => mockRealDebridClient.Object);
            });
        });
        
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task UploadMagnet_WhenRealDebridReportsInfringing_ShouldReturn400BadRequest()
    {
        // Arrange
        var request = new
        {
            MagnetLink = "magnet:?xt=urn:btih:test123&dn=Test+Torrent",
            Torrent = new
            {
                Category = "sonarr",
                DownloadClient = "qBittorrent"
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/Api/Torrents/UploadMagnet", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Failed to add torrent", responseContent);
        Assert.Contains("Real-Debrid rejected this torrent as infringing", responseContent);
    }

    [Fact]
    public async Task UploadFile_WhenRealDebridReportsVirus_ShouldReturn400BadRequest()
    {
        // Arrange
        var formData = new MultipartFormDataContent();
        
        // Create a dummy torrent file
        var torrentBytes = Encoding.UTF8.GetBytes("dummy torrent content");
        var torrentContent = new ByteArrayContent(torrentBytes);
        torrentContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-bittorrent");
        formData.Add(torrentContent, "file", "test.torrent");

        var torrentData = new
        {
            Torrent = new
            {
                Category = "sonarr",
                DownloadClient = "qBittorrent"
            }
        };
        
        var torrentJson = JsonSerializer.Serialize(torrentData);
        formData.Add(new StringContent(torrentJson), "formData");

        // Act
        var response = await _client.PostAsync("/Api/Torrents/UploadFile", formData);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Failed to add torrent", responseContent);
    }

    [Fact]
    public async Task UploadMagnet_WhenRealDebridReportsNotCached_ShouldReturn400BadRequest()
    {
        // Arrange - Update the mock to return not_cached status
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mockRealDebridClient = new Mock<RealDebridTorrentClient>();
                
                mockRealDebridClient.Setup(x => x.GetTorrentInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new TorrentClientTorrent { Status = "not_cached" });
                
                services.AddScoped<RealDebridTorrentClient>(_ => mockRealDebridClient.Object);
            });
        });
        
        var client = factory.CreateClient();
        
        var request = new
        {
            MagnetLink = "magnet:?xt=urn:btih:test456&dn=Test+Torrent+Not+Cached",
            Torrent = new
            {
                Category = "sonarr",
                DownloadClient = "qBittorrent"
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/Api/Torrents/UploadMagnet", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadMagnet_WhenRealDebridReportsDownloaded_ShouldReturn200OK()
    {
        // Arrange - Update the mock to return downloaded status
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mockRealDebridClient = new Mock<RealDebridTorrentClient>();
                
                mockRealDebridClient.Setup(x => x.GetTorrentInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new TorrentClientTorrent { Status = "downloaded" });
                
                services.AddScoped<RealDebridTorrentClient>(_ => mockRealDebridClient.Object);
            });
        });
        
        var client = factory.CreateClient();
        
        var request = new
        {
            MagnetLink = "magnet:?xt=urn:btih:test789&dn=Test+Torrent+Downloaded",
            Torrent = new
            {
                Category = "sonarr",
                DownloadClient = "qBittorrent"
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/Api/Torrents/UploadMagnet", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// ProblemDetails class for deserializing HTTP problem responses
/// </summary>
public class ProblemDetails
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public Dictionary<string, object>? Extensions { get; set; }
}