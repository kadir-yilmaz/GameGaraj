#!/bin/bash
# ==============================================================================
# GameGaraj — Elasticsearch ILM Setup Script
# ==============================================================================
# This script creates the ILM (Index Lifecycle Management) policy and 
# index template for GameGaraj log indices.
#
# Usage: ./setup-elasticsearch-ilm.sh [ELASTICSEARCH_URL]
# Default: http://localhost:9201
# ==============================================================================

ELASTIC_URL="${1:-http://localhost:9201}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ILM_POLICY_FILE="$SCRIPT_DIR/../config/elasticsearch/ilm-policy.json"

echo "============================================"
echo "GameGaraj Elasticsearch ILM Setup"
echo "Elasticsearch URL: $ELASTIC_URL"
echo "============================================"

# 1. Check Elasticsearch connectivity
echo ""
echo "→ Checking Elasticsearch connectivity..."
if ! curl -s -o /dev/null -w "%{http_code}" "$ELASTIC_URL" | grep -q "200"; then
    echo "✗ Cannot connect to Elasticsearch at $ELASTIC_URL"
    exit 1
fi
echo "✓ Elasticsearch is reachable"

# 2. Create ILM Policy
echo ""
echo "→ Creating ILM policy 'gamegaraj-logs-policy'..."
curl -s -X PUT "$ELASTIC_URL/_ilm/policy/gamegaraj-logs-policy" \
    -H "Content-Type: application/json" \
    -d @"$ILM_POLICY_FILE"
echo ""
echo "✓ ILM policy created"

# 3. Create Index Template that references the ILM policy
echo ""
echo "→ Creating index template 'gamegaraj-logs-template'..."
curl -s -X PUT "$ELASTIC_URL/_index_template/gamegaraj-logs-template" \
    -H "Content-Type: application/json" \
    -d '{
  "index_patterns": ["gamegaraj-logs-*"],
  "template": {
    "settings": {
      "index.lifecycle.name": "gamegaraj-logs-policy",
      "index.lifecycle.rollover_alias": "gamegaraj-logs",
      "number_of_shards": 1,
      "number_of_replicas": 0
    }
  },
  "priority": 500,
  "composed_of": [],
  "_meta": {
    "description": "GameGaraj log indices with ILM lifecycle management"
  }
}'
echo ""
echo "✓ Index template created"

# 4. Verify
echo ""
echo "→ Verifying setup..."
echo "  ILM Policy:"
curl -s "$ELASTIC_URL/_ilm/policy/gamegaraj-logs-policy" | python3 -m json.tool 2>/dev/null || echo "(install python3 for pretty output)"
echo ""
echo "  Index Template:"
curl -s "$ELASTIC_URL/_index_template/gamegaraj-logs-template" | python3 -m json.tool 2>/dev/null || echo "(install python3 for pretty output)"

echo ""
echo "============================================"
echo "✓ ILM setup complete!"
echo ""
echo "Log retention policy:"
echo "  Hot:    0 - 7 days  (rollover at 1 day or 5GB)"
echo "  Warm:   7 - 30 days (shrink + forcemerge)"
echo "  Cold:  30 - 180 days"
echo "  Delete: after 180 days"
echo "============================================"
