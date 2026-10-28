@echo off
chcp 65001 > nul
echo ===================================================
echo 🚀 Starting EasyOrders C# API + WhatsApp Gateway + Permanent Domain...
echo ===================================================

start "EasyOrder C# Webhook API (Port 5055)" cmd /k "cd /d %~dp0 && dotnet run --urls http://localhost:5055"
start "WhatsApp Free Gateway + Static Link (Port 3001)" cmd /k "cd /d %~dp0WhatsAppGateway && node server.js"

echo ✅ All services launched!
echo 🌐 Control Panel & QR Page: http://localhost:3001
echo 🔗 Permanent EasyOrders URL: https://outlix-easyorder-yasmin.loca.lt/api/webhook/easyorder
pause
