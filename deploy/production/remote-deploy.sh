#!/usr/bin/env bash
set -euo pipefail

: "${DEPLOY_APP_DIR:?DEPLOY_APP_DIR is required}"
: "${DEPLOY_DOMAIN:?DEPLOY_DOMAIN is required}"
: "${GHCR_USERNAME:?GHCR_USERNAME is required}"
: "${GHCR_TOKEN:?GHCR_TOKEN is required}"
: "${IMAGE_REF:?IMAGE_REF is required}"

compose_file="$DEPLOY_APP_DIR/docker-compose.yml"
nginx_dir="$DEPLOY_APP_DIR/nginx"
rendered_nginx_conf="$nginx_dir/$DEPLOY_DOMAIN.conf"
site_available="/etc/nginx/sites-available/$DEPLOY_DOMAIN.conf"
site_enabled="/etc/nginx/sites-enabled/$DEPLOY_DOMAIN.conf"
cert_dir="/etc/ssl/cloudflare/$DEPLOY_DOMAIN"
cert_path="$cert_dir/origin.crt"
key_path="$cert_dir/origin.key"

mkdir -p "$DEPLOY_APP_DIR/logs" "$DEPLOY_APP_DIR/data-protection-keys" "$nginx_dir"

printf '%s\n' "$GHCR_TOKEN" | docker login ghcr.io --username "$GHCR_USERNAME" --password-stdin

IMAGE_REF="$IMAGE_REF" docker compose -f "$compose_file" pull
IMAGE_REF="$IMAGE_REF" docker compose -f "$compose_file" up -d --remove-orphans

nginx_template="$nginx_dir/app-http.conf.template"

if sudo test -f "$cert_path" && sudo test -f "$key_path"; then
  nginx_template="$nginx_dir/app-https.conf.template"
fi

sed \
  -e "s|__DEPLOY_DOMAIN__|$DEPLOY_DOMAIN|g" \
  -e "s|__TLS_CERT__|$cert_path|g" \
  -e "s|__TLS_KEY__|$key_path|g" \
  "$nginx_template" > "$rendered_nginx_conf"

sudo install -m 644 "$rendered_nginx_conf" "$site_available"
sudo ln -sfn "$site_available" "$site_enabled"
sudo nginx -t
sudo systemctl reload nginx
