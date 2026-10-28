@echo off
chcp 65001 > nul
echo ===================================================
echo 🚀 Starting EasyOrders C# API, WhatsApp Gateway, and Cloudflare Tunnel...
echo ===================================================

start "WhatsApp Free Gateway (Port 3001)" cmd /k "cd /d %~dp0WhatsAppGateway && node server.js"
start "EasyOrder C# Webhook API (Port 5055)" cmd /k "cd /d %~dp0 && dotnet run --urls http://localhost:5055"
start "Cloudflare HTTPS Tunnel" cmd /k "cd /d %~dp0 && cloudflared.exe tunnel --url http://localhost:5055"

echo ✅ All 3 services launched!
echo 🌐 WhatsApp QR Page: http://localhost:3001
echo 🌐 C# Webhook API:   http://localhost:5055/api/webhook/easyorder
pause
