# syntax=docker/dockerfile:1

# Use the SDK image to run the app (build on container start)
FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /app

# Copy the whole repo (solution + appsettings.json)
COPY . .

# Ensure we run from the project folder
WORKDIR /app/Challenge

# Optional: make sure appsettings.json gets copied when running locally too
# (Also add CopyToOutputDirectory in the csproj; see note below.)

# Run the console app; config is read from appsettings.json + env vars
ENTRYPOINT ["dotnet", "run", "--"]