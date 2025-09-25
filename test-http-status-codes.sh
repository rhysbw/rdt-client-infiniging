#!/bin/bash

# Test script to verify RdtClient returns correct HTTP status codes
# This simulates what Sonarr would receive

echo "Testing RdtClient HTTP Status Codes for Sonarr Integration"
echo "=========================================================="

# Configuration
RDT_CLIENT_URL="http://localhost:6500"
TEST_MAGNET="magnet:?xt=urn:btih:test123&dn=Test+Torrent"

echo ""
echo "Test 1: Valid Torrent (Should return HTTP 200)"
echo "----------------------------------------------"
curl -X POST "$RDT_CLIENT_URL/Api/Torrents/UploadMagnet" \
  -H "Content-Type: application/json" \
  -H "Authorization: Basic $(echo -n 'admin:admin' | base64)" \
  -d "{
    \"MagnetLink\": \"$TEST_MAGNET\",
    \"Torrent\": {
      \"Category\": \"sonarr\",
      \"DownloadClient\": \"qBittorrent\"
    }
  }" \
  -w "\nHTTP Status: %{http_code}\n" \
  -s

echo ""
echo "Test 2: Infringing Torrent (Should return HTTP 400)"
echo "---------------------------------------------------"
# Note: This will only return 400 if Real-Debrid actually reports infringing
# In a real test, you'd need a torrent that RD flags as infringing
curl -X POST "$RDT_CLIENT_URL/Api/Torrents/UploadMagnet" \
  -H "Content-Type: application/json" \
  -H "Authorization: Basic $(echo -n 'admin:admin' | base64)" \
  -d "{
    \"MagnetLink\": \"magnet:?xt=urn:btih:infringing123&dn=Infringing+Content\",
    \"Torrent\": {
      \"Category\": \"sonarr\",
      \"DownloadClient\": \"qBittorrent\"
    }
  }" \
  -w "\nHTTP Status: %{http_code}\n" \
  -s

echo ""
echo "Expected Results:"
echo "- HTTP 200: Sonarr will process the torrent normally"
echo "- HTTP 400: Sonarr will try the next download client (qBittorrent) - matches qBittorrent API behavior"
echo "- HTTP 409: Sonarr will blacklist the release (WRONG BEHAVIOR)"
echo ""
echo "Check Sonarr Activity tab to verify behavior!"