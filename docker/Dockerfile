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

# Install .NET EF Core tools for running migrations
RUN dotnet tool install --global dotnet-ef --version 10.0.0
ENV PATH="$PATH:/root/.dotnet/tools"

COPY --from=publish /app/publish .

# Create thumbnail and logs directories
RUN mkdir -p /mnt/gallery/.thumbnails && \
    mkdir -p /app/logs

ENTRYPOINT ["dotnet", "KelliPhoto.Web.dll"]
