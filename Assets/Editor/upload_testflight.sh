#!/bin/bash
set -e

echo "=== TestFlight Upload Script ==="

# Check required env vars
if [ -z "$ASC_KEY_ID" ] || [ -z "$ASC_ISSUER_ID" ] || [ -z "$ASC_PRIVATE_KEY_B64" ]; then
    echo "ERROR: Missing required environment variables."
    echo "Required: ASC_KEY_ID, ASC_ISSUER_ID, ASC_PRIVATE_KEY_B64"
    exit 1
fi

# Create Fastlane API key JSON using Python (handles newline escaping properly)
# Fastlane requires "key" field with actual PEM content, not key_filepath
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

print("API key JSON created successfully")
PYEOF

# Find IPA using OUTPUT_DIRECTORY env var (set by Unity Build Automation)
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
echo "Uploading to TestFlight..."

fastlane pilot upload \
    --ipa "$IPA_FILE" \
    --api_key_path /tmp/asc_key.json \
    --skip_waiting_for_build_processing true \
    --app_identifier "com.dogus.pixelflowclone"

echo "=== Upload to TestFlight successful! ==="

# Cleanup
rm -f /tmp/asc_key.json
