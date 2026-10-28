# Stage 1: Build ASP.NET Core C# API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["EasyOrderWhatsAppApi.csproj", "./"]
RUN dotnet restore "EasyOrderWhatsAppApi.csproj"
COPY . .
RUN dotnet publish "EasyOrderWhatsAppApi.csproj" -c Release -o /app/publish

# Stage 2: Runtime with .NET 8 + Node.js 20 for WhatsApp Gateway
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# تثبيت Node.js لتشغيل بوابة الواتساب المجانية بجانب الـ C# API
RUN apt-get update && apt-get install -y curl gnupg && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    rm -rf /var/lib/apt/lists/*

# نسخ ملفات الـ C# API
COPY --from=build /app/publish .

# نسخ وتثبيت بوابة الواتساب
WORKDIR /app/WhatsAppGateway
COPY WhatsAppGateway/package.json ./
RUN npm install --omit=dev
COPY WhatsAppGateway/server.js ./

WORKDIR /app
COPY start-cloud.sh ./
RUN sed -i 's/\r$//' start-cloud.sh && chmod +x start-cloud.sh

ENV PORT=8080
EXPOSE 8080

CMD ["./start-cloud.sh"]
