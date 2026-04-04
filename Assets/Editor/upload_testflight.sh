#!/bin/bash
set -e

echo "=== TestFlight Upload Script ==="

# Check required env vars
if [ -z "$ASC_KEY_ID" ] || [ -z "$ASC_ISSUER_ID" ] || [ -z "$ASC_PRIVATE_KEY_B64" ]; then
    echo "ERROR: Missing required environment variables."
    exit 1
fi

# Decode private key
echo "$ASC_PRIVATE_KEY_B64" | base64 --decode > /tmp/AuthKey.p8
chmod 600 /tmp/AuthKey.p8

# Place key where altool expects it
mkdir -p ~/private_keys
cp /tmp/AuthKey.p8 ~/private_keys/AuthKey_${ASC_KEY_ID}.p8

# Also create for ~/.private_keys (altool alternate location)
mkdir -p ~/.private_keys
cp /tmp/AuthKey.p8 ~/.private_keys/AuthKey_${ASC_KEY_ID}.p8

# Find IPA
IPA_FILE=""
if [ -n "$OUTPUT_DIRECTORY" ]; then
    IPA_FILE=$(find "$OUTPUT_DIRECTORY" -name "*.ipa" 2>/dev/null | head -1)
fi
if [ -z "$IPA_FILE" ]; then
    IPA_FILE=$(find "$HOME" -name "*.ipa" 2>/dev/null | grep -v ".Trash" | head -1)
fi
if [ -z "$IPA_FILE" ]; then
    echo "ERROR: No IPA file found!"
    exit 1
fi

echo "Found IPA: $IPA_FILE"
echo "IPA size: $(du -h "$IPA_FILE" | cut -f1)"
echo ""

# Method 1: Try xcrun altool (gives detailed validation errors)
echo "=== Uploading via xcrun altool ==="
xcrun altool --upload-app \
    --type ios \
    --file "$IPA_FILE" \
    --apiKey "$ASC_KEY_ID" \
    --apiIssuer "$ASC_ISSUER_ID" \
    --verbose 2>&1 || ALTOOL_FAILED=1

if [ -n "$ALTOOL_FAILED" ]; then
    echo ""
    echo "=== altool failed, trying Fastlane as fallback ==="

    # Create Fastlane API key JSON
    python3 << 'PYEOF'
import json, base64, os
key_b64 = os.environ['ASC_PRIVATE_KEY_B64']
key_content = base64.b64decode(key_b64).decode('utf-8')
data = {
    "key_id": os.environ['ASC_KEY_ID'],
    "issuer_id": os.environ['ASC_ISSUER_ID'],
    "key": key_content,
    "in_house": False
}
with open('/tmp/asc_key.json', 'w') as f:
    json.dump(data, f)
PYEOF

    fastlane pilot upload \
        --ipa "$IPA_FILE" \
        --api_key_path /tmp/asc_key.json \
        --skip_waiting_for_build_processing true \
        --app_identifier "com.dogus.pixelflowclone" \
        --verbose
fi

echo "=== Upload script finished ==="

# Cleanup
rm -f /tmp/AuthKey.p8 /tmp/asc_key.json
rm -f ~/private_keys/AuthKey_${ASC_KEY_ID}.p8
rm -f ~/.private_keys/AuthKey_${ASC_KEY_ID}.p8
