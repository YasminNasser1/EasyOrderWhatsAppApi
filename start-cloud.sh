#!/bin/bash
echo "🚀 Starting Free WhatsApp Gateway on port 3001..."
cd /app/WhatsAppGateway && node server.js &

echo "🚀 Starting EasyOrders C# Webhook API..."
cd /app && dotnet EasyOrderWhatsAppApi.dll
