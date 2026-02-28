#!/bin/sh
set -eu

backend_url="${WIZARD_BACKEND_URL:-http://localhost:4646}"

escape_for_js() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

backend_url_escaped="$(escape_for_js "$backend_url")"

cat > /usr/share/nginx/html/runtime-config.js <<EOF
window.__WIZARD_CONFIG__ = {
  backendUrl: "$backend_url_escaped"
};
EOF
