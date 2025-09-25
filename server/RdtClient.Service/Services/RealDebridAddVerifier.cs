using Microsoft.Extensions.Logging;
using RdtClient.Data.Models.Internal;
using RdtClient.Service.Exceptions;
using RdtClient.Service.Services.TorrentClients;

namespace RdtClient.Service.Services;

/// <summary>
/// Verifies the initial status of a Real-Debrid torrent after adding it to determine
/// if it should fail fast due to fatal statuses like infringing, virus, etc.
/// </summary>
public class RealDebridAddVerifier(ILogger<RealDebridAddVerifier> logger, RealDebridTorrentClient realDebridClient)
{
    /// <summary>
    /// Verifies the initial status of a torrent and throws an exception if it should fail fast.
    /// </summary>
    /// <param name="torrentId">The Real-Debrid torrent ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="RealDebridFailFastException">Thrown when the torrent has a fatal status</exception>
    public async Task VerifyInitialStatusOrThrowAsync(string torrentId, CancellationToken cancellationToken = default)
    {
        var settings = Settings.Get.Provider;
        
        if (!settings.FailOnInfringing && !settings.FailOnUncached)
        {
            // Fail-fast is disabled, proceed normally
            return;
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(settings.AddCheckTimeoutMs);
        var delay = TimeSpan.FromMilliseconds(250);

        logger.LogDebug("Starting fail-fast verification for torrent {TorrentId} with timeout {TimeoutMs}ms", 
                       torrentId, settings.AddCheckTimeoutMs);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var torrentInfo = await realDebridClient.GetTorrentInfoAsync(torrentId, cancellationToken);
                var status = NormalizeStatus(torrentInfo?.Status);

                logger.LogDebug("Torrent {TorrentId} status: {Status}", torrentId, status);

                if (IsFatal(status))
                {
                    logger.LogWarning("Torrent {TorrentId} has fatal status '{Status}', failing fast", torrentId, status);
                    
                    // Attempt cleanup on Real-Debrid (best effort)
                    try
                    {
                        await realDebridClient.DeleteAsync(torrentId, cancellationToken);
                        logger.LogDebug("Successfully cleaned up torrent {TorrentId} from Real-Debrid", torrentId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to cleanup torrent {TorrentId} from Real-Debrid", torrentId);
                    }

                    throw new RealDebridFailFastException(status, $"Real-Debrid rejected this torrent as {status}");
                }

                if (IsClearlyOk(status))
                {
                    logger.LogDebug("Torrent {TorrentId} has acceptable status '{Status}', proceeding", torrentId, status);
                    return;
                }

                // Status is neither fatal nor clearly OK, continue polling
                await Task.Delay(delay, cancellationToken);
                
                // Simple back-off up to ~1s
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 1000));
            }
            catch (RealDebridFailFastException)
            {
                // Re-throw our custom exception
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error checking status for torrent {TorrentId}, continuing verification", torrentId);
                
                // On error, wait a bit and continue
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 1000));
            }
        }

        // No fatal state within window: accept, let background poller handle later transitions
        logger.LogDebug("Torrent {TorrentId} verification timeout reached, proceeding with normal flow", torrentId);
    }

    /// <summary>
    /// Normalizes the status string from Real-Debrid API
    /// </summary>
    private static string NormalizeStatus(string? status)
    {
        return status?.ToLowerInvariant() ?? "unknown";
    }

    /// <summary>
    /// Determines if a status is fatal and should cause immediate failure
    /// </summary>
    private bool IsFatal(string status)
    {
        var settings = Settings.Get.Provider;
        
        // Always fatal statuses
        if (status.Equals("infringing", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("virus", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("deleted", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("banned", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("magnet_error", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Conditionally fatal statuses
        if (settings.FailOnUncached && status.Equals("not_cached", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a status is clearly acceptable and we can proceed
    /// </summary>
    private static bool IsClearlyOk(string status)
    {
        return status.Equals("downloaded", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("queued", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("downloading", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("magnet_conversion", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("waiting_files_selection", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("compressing", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("uploading", StringComparison.OrdinalIgnoreCase);
    }
}