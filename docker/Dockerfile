# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG APPLICATION_VERSION
ARG GIT_COMMIT
ARG BUILD_TIMESTAMP
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/KelliPhoto.Web/KelliPhoto.Web.csproj", "src/KelliPhoto.Web/"]
COPY ["Directory.Build.props", "./"]
COPY ["VERSION", "./"]
RUN dotnet restore "src/KelliPhoto.Web/KelliPhoto.Web.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/KelliPhoto.Web"
RUN dotnet build "KelliPhoto.Web.csproj" -c Release -o /app/build \
    /p:ApplicationVersion="${APPLICATION_VERSION:-}" \
    /p:GitCommit="${GIT_COMMIT:-}" \
    /p:BuildTimestamp="${BUILD_TIMESTAMP:-}"

# Publish stage
FROM build AS publish
ARG APPLICATION_VERSION
ARG GIT_COMMIT
ARG BUILD_TIMESTAMP
RUN dotnet publish "KelliPhoto.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false \
    /p:ApplicationVersion="${APPLICATION_VERSION:-}" \
    /p:GitCommit="${GIT_COMMIT:-}" \
    /p:BuildTimestamp="${BUILD_TIMESTAMP:-}"

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Run EF migrations from SDK (dev/CI), not in this runtime image.
# Required by providers that load Kerberos/GSSAPI runtime libraries.
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Writable caches (when GallerySettings point here); gallery mount may be read-only in production.
RUN mkdir -p /app/data/thumbnails /app/data/web /app/logs

ENTRYPOINT ["dotnet", "KelliPhoto.Web.dll"]
