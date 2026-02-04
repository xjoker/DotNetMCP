#!/bin/bash
# DotNetMCP All-in-One Startup Script

set -e

echo "============================================"
echo "  DotNetMCP All-in-One Container Starting"
echo "============================================"
echo "Backend API:  http://0.0.0.0:8650"
echo "MCP Server:   http://0.0.0.0:8651"
echo "============================================"

# Ensure directories exist
mkdir -p /data/assemblies /app/cache /var/log/supervisor

# Start supervisord
exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf
