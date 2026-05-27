#!/usr/bin/env bash
## Provision a new edge load-balancer instance via the OSB API and poll until ready.
## Usage: ./scripts/provision-example.sh [instance-id] [hostname]
set -euo pipefail

BROKER=${BROKER:-http://localhost:8080}
INSTANCE_ID=${1:-demo-$(date +%s)}
HOSTNAME=${2:-api.example.com}
SERVICE_ID=svc-edge-lb
PLAN_ID=standard

echo ">> Provisioning ${INSTANCE_ID} (hostname: ${HOSTNAME})"

curl -fsS -X PUT \
  "${BROKER}/v2/service_instances/${INSTANCE_ID}?accepts_incomplete=true" \
  -H "X-Broker-API-Version: 2.17" \
  -H "Content-Type: application/json" \
  -d @- <<JSON
{
  "service_id": "${SERVICE_ID}",
  "plan_id": "${PLAN_ID}",
  "parameters": {
    "upstream_service": "my-service.default.svc.cluster.local",
    "upstream_port": 8080,
    "hostname": "${HOSTNAME}",
    "listener_port": 443
  }
}
JSON

echo
echo ">> Polling /last_operation until terminal..."
for i in {1..30}; do
  STATE=$(curl -fsS \
    "${BROKER}/v2/service_instances/${INSTANCE_ID}/last_operation" \
    -H "X-Broker-API-Version: 2.17" | python3 -c "import sys, json; print(json.load(sys.stdin)['state'])")
  echo "[$i] state=${STATE}"
  case "$STATE" in
    succeeded) echo ">> Provisioned."; exit 0 ;;
    failed)    echo ">> Provision failed."; exit 1 ;;
  esac
  sleep 1
done

echo ">> Timed out waiting for provision."
exit 1
