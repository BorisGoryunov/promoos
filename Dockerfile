FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# НЕ ставим USER здесь. В образе .NET 10 есть пользователь "app" (UID 1654).
# Если хотите UID 1000 — создайте его явно, но проще использовать "app".
WORKDIR /app
EXPOSE 8080
# Явно задаём порт
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Promoos.Server/Promoos.Server.csproj", "src/Promoos.Server/"]
RUN dotnet restore "src/Promoos.Server/Promoos.Server.csproj"

COPY . .
WORKDIR "/src/src/Promoos.Server"
RUN dotnet build "Promoos.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Promoos.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Переключаемся на непривилегированного пользователя ПОСЛЕ копирования,
# и даём ему права на файлы
USER app
ENTRYPOINT ["dotnet", "Promoos.Server.dll"]