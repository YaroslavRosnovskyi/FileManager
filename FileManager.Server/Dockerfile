# Use the official .NET 9 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["FileManager.Server/FileManager.Server.csproj", "FileManager.Server/"]
COPY ["FileManager.Core/FileManager.Core.csproj", "FileManager.Core/"]
COPY ["FileManager.Proto/FileManager.Proto.csproj", "FileManager.Proto/"]

# Restore dependencies
RUN dotnet restore "FileManager.Server/FileManager.Server.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/FileManager.Server"
RUN dotnet build "FileManager.Server.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "FileManager.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create Storage and https directories
RUN mkdir -p /app/Storage
RUN mkdir -p /app/https

# Copy the certificate from the build context
COPY ["FileManager.Server/https/aspnetapp.pfx", "/app/https/aspnetapp.pfx"]

ENTRYPOINT ["dotnet", "FileManager.Server.dll"]