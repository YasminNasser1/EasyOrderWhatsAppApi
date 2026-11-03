@echo off
:: 1. تشغيل سيرفر الـ C# (بورت 5055) في الخلفية لو مش شغال
netstat -ano | findstr :5055 | findstr LISTENING >nul
if %errorlevel% neq 0 (
    cd /d "C:\Users\EL-Mostwred\source\repos\EasyOrderWhatsAppApi"
    start "" /B dotnet run -c Release --project "C:\Users\EL-Mostwred\source\repos\EasyOrderWhatsAppApi\EasyOrderWhatsAppApi.csproj" --urls "http://localhost:5055"
)

:: 2. تشغيل بوابة الواتساب (بورت 3001) في الخلفية لو مش شغالة
netstat -ano | findstr :3001 | findstr LISTENING >nul
if %errorlevel% neq 0 (
    cd /d "C:\Users\EL-Mostwred\source\repos\EasyOrderWhatsAppApi\WhatsAppGateway"
    start "" /B node server.js
)

:: 3. الانتظار 4 ثواني لحد ما السيرفر يقوم وبعدين فتح صفحة الواتساب تلقائياً في المتصفح
timeout /t 4 /nobreak >nul
start "" "http://localhost:3001"
