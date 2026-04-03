#!/bin/bash
set -e

echo "=== TestFlight Upload Script ==="

# Check required env vars
if [ -z "$ASC_KEY_ID" ] || [ -z "$ASC_ISSUER_ID" ] || [ -z "$ASC_PRIVATE_KEY_B64" ]; then
    echo "ERROR: Missing required environment variables."
    echo "Required: ASC_KEY_ID, ASC_ISSUER_ID, ASC_PRIVATE_KEY_B64"
    exit 1
fi

# Decode private key from base64
echo "$ASC_PRIVATE_KEY_B64" | base64 --decode > /tmp/AuthKey.p8
chmod 600 /tmp/AuthKey.p8

# Create Fastlane API key JSON
cat > /tmp/asc_key.json << JSONEOF
{
  "key_id": "${ASC_KEY_ID}",
  "issuer_id": "${ASC_ISSUER_ID}",
  "key_filepath": "/tmp/AuthKey.p8",
  "in_house": false
}
JSONEOF

echo "API key written. Searching for IPA..."

# Find the IPA (Unity Build Automation exports it to the build directory)
IPA_FILE=$(find /Users -name "*.ipa" 2>/dev/null | grep -v ".Trash" | head -1)

if [ -z "$IPA_FILE" ]; then
    IPA_FILE=$(find "$HOME" -name "*.ipa" 2>/dev/null | head -1)
fi

if [ -z "$IPA_FILE" ]; then
    echo "ERROR: No IPA file found!"
    exit 1
fi

echo "Found IPA: $IPA_FILE"
echo "Uploading to TestFlight..."

fastlane pilot upload \
    --ipa "$IPA_FILE" \
    --api_key_path /tmp/asc_key.json \
    --skip_waiting_for_build_processing true \
    --app_identifier "com.dogus.pixelflowclone"

echo "=== Upload to TestFlight successful! ==="

# Cleanup
rm -f /tmp/AuthKey.p8 /tmp/asc_key.json
