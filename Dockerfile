FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["JIRAbot.csproj", "./"]
RUN dotnet restore "JIRAbot.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "JIRAbot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "JIRAbot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Установка Chromium и необходимых библиотек
RUN apt-get update && apt-get install -y \
    chromium \
    fonts-liberation \
    libasound2 \
    libatk-bridge2.0-0 \
    libcups2 \
    libnss3 \
    libx11-xcb1 \
    libxcomposite1 \
    libxrandr2 \
    libappindicator3-1 \
    xdg-utils \
    libgbm-dev \
    --no-install-recommends && apt-get clean && rm -rf /var/lib/apt/lists/* 

RUN mkdir -p /app/.chrome && chmod -R 777 /app/.chrome

# Определение пользователя, если требуется
USER $APP_UID

ENTRYPOINT ["dotnet", "JIRAbot.dll"]