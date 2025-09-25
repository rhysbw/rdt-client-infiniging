# Testing Sonarr Download Client Fallback Behavior

## Test Scenario: HTTP Status Code Impact on Sonarr

This document outlines how to test that Sonarr properly falls back to the next download client when RdtClient returns HTTP 400 (Bad Request) for infringing torrents, matching qBittorrent's API behavior.

## Expected Sonarr Behavior

### HTTP 400 (Bad Request) - ✅ CORRECT
- **Sonarr Response**: "Failed to add torrent"
- **Action**: Try the same release with the next download client in priority list
- **Result**: Sonarr attempts qBittorrent (or next client) for the same release

### HTTP 409 (Conflict) - ❌ WRONG
- **Sonarr Response**: "Download client rejected release" 
- **Action**: Blacklist the release and try a different release
- **Result**: Sonarr searches for a completely different release

## Test Setup

### 1. Configure Sonarr with Multiple Download Clients

In Sonarr Settings > Download Clients:

1. **Primary Client**: RdtClient (Priority 1)
   - Host: `http://your-rdt-client:6500`
   - Username/Password: Your RdtClient credentials
   - Category: `sonarr`

2. **Fallback Client**: qBittorrent (Priority 2)
   - Host: `http://your-qbittorrent:8080`
   - Username/Password: Your qBittorrent credentials
   - Category: `sonarr`

### 2. Enable Fail-Fast in RdtClient

In RdtClient Settings > Provider:
- ✅ **Fail on infringing**: `true`
- ❌ **Fail on uncached**: `false` (optional)
- ⏱️ **Add check timeout (ms)**: `4000`

### 3. Test Scenarios

#### Test 1: Infringing Torrent (Should Return HTTP 400)
1. Find a torrent that Real-Debrid will flag as infringing
2. Add it to Sonarr
3. **Expected**: RdtClient returns HTTP 400, Sonarr tries qBittorrent for same release

#### Test 2: Valid Torrent (Should Return HTTP 200)
1. Find a torrent that Real-Debrid accepts
2. Add it to Sonarr  
3. **Expected**: RdtClient returns HTTP 200, torrent processes normally

#### Test 3: Uncached Torrent (Behavior depends on setting)
1. Find a torrent that's not cached on Real-Debrid
2. Add it to Sonarr
3. **If Fail on uncached = false**: RdtClient returns HTTP 200, torrent queues
4. **If Fail on uncached = true**: RdtClient returns HTTP 400, Sonarr tries qBittorrent

## Verification Steps

### Check Sonarr Activity Tab
1. Go to Sonarr > Activity
2. Look for the test torrent
3. **HTTP 400 Response**: Should show "Failed to add torrent"
4. **HTTP 200 Response**: Should show normal processing

### Check Download Client Status
1. Go to Sonarr > Settings > Download Clients
2. Click "Test" on RdtClient
3. Should show "Connection successful" (client is working, just rejecting specific torrents)

### Check qBittorrent
1. Open qBittorrent web interface
2. Look for the torrent that was rejected by RdtClient
3. Should appear in qBittorrent's queue if HTTP 400 was returned

## Troubleshooting

### If Sonarr Blacklists Instead of Trying Next Client
- **Problem**: RdtClient is returning HTTP 409 instead of HTTP 400
- **Solution**: Check RdtClient logs, ensure fail-fast is properly configured

### If No Fallback Happens
- **Problem**: qBittorrent not configured or not working
- **Solution**: Test qBittorrent connection in Sonarr settings

### If RdtClient Doesn't Detect Infringing
- **Problem**: Timeout too short or Real-Debrid API slow
- **Solution**: Increase "Add check timeout (ms)" to 6000-8000

## Log Analysis

### RdtClient Logs
Look for these log entries:
```
[WARN] Fail-fast RD rejection: infringing
[DEBUG] Torrent {torrentId} has fatal status 'infringing', failing fast
[DEBUG] Successfully cleaned up torrent {torrentId} from Real-Debrid
```

### Sonarr Logs
Look for these log entries:
```
[WARN] Download client temporarily unavailable: Real-Debrid rejected this torrent as infringing
[INFO] Trying next download client for release: {releaseName}
```

## Success Criteria

✅ **Test Passes When**:
- RdtClient returns HTTP 400 for infringing torrents (matching qBittorrent behavior)
- Sonarr tries qBittorrent for the same release (not a different release)
- No blacklisting occurs for the release
- Valid torrents still work normally through RdtClient

❌ **Test Fails When**:
- RdtClient returns HTTP 409 (causes blacklisting)
- Sonarr searches for different releases instead of trying next client
- No fallback to qBittorrent occurs
- Valid torrents are also rejected