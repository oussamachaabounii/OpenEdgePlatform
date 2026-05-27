FROM envoyproxy/envoy:v1.29-latest

COPY envoy-bootstrap.yaml /etc/envoy/envoy-bootstrap.yaml

ENV REGION=us-east-1 \
    POD_NAME=envoy-edge \
    CONTROL_PLANE_HOST=control-plane \
    CONTROL_PLANE_PORT=18000

CMD ["envoy", "-c", "/etc/envoy/envoy-bootstrap.yaml", "--service-cluster", "edge-fleet", "--service-node", "$POD_NAME"]
