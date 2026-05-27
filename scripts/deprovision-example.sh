#!/usr/bin/env bash
## Deprovision an edge load-balancer instance via the OSB API.
## Usage: ./scripts/deprovision-example.sh <instance-id>
set -euo pipefail

BROKER=${BROKER:-http://localhost:8080}
INSTANCE_ID=${1:?instance id required}

echo ">> Deprovisioning ${INSTANCE_ID}"

curl -fsS -X DELETE \
  "${BROKER}/v2/service_instances/${INSTANCE_ID}?service_id=svc-edge-lb&plan_id=standard&accepts_incomplete=true" \
  -H "X-Broker-API-Version: 2.17"

echo
echo ">> Polling until terminal..."
for i in {1..30}; do
  RESP=$(curl -sS -o /dev/null -w "%{http_code}" \
    "${BROKER}/v2/service_instances/${INSTANCE_ID}/last_operation" \
    -H "X-Broker-API-Version: 2.17")
  echo "[$i] http=${RESP}"
  if [ "$RESP" = "410" ]; then
    echo ">> Deprovisioned."
    exit 0
  fi
  sleep 1
done

echo ">> Timed out waiting for deprovision."
exit 1
