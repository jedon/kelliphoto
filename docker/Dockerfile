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

# ImageSharp doesn't require libgdiplus on .NET Core/5+ (it's a pure .NET library)
# If you need System.Drawing support, uncomment the following:
# RUN apt-get update && \
#     apt-get install -y --no-install-recommends libgdiplus libc6-dev && \
#     rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Create thumbnail directory
RUN mkdir -p /mnt/gallery/.thumbnails

ENTRYPOINT ["dotnet", "KelliPhoto.Web.dll"]
