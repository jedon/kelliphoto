# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/KelliPhoto.Web/KelliPhoto.Web.csproj", "src/KelliPhoto.Web/"]
RUN dotnet restore "src/KelliPhoto.Web/KelliPhoto.Web.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/KelliPhoto.Web"
RUN dotnet build "KelliPhoto.Web.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "KelliPhoto.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Run EF migrations from SDK (dev/CI), not in this runtime image.

COPY --from=publish /app/publish .

# Writable caches (when GallerySettings point here); gallery mount may be read-only in production.
RUN mkdir -p /app/data/thumbnails /app/data/web /app/logs

ENTRYPOINT ["dotnet", "KelliPhoto.Web.dll"]
