# =========================
# Stage 1: Build the function
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy project files and restore dependencies
COPY *.csproj .
RUN dotnet restore

# Copy all source files and publish
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:GenerateFunctions=true
# ------------------------------------------------------------
# ------------------------------------------------------------
    #Running Stage
# ------------------------------------------------------------

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0

# install ffmpeg (Debian/Ubuntu)
RUN apt-get update && apt-get install -y ffmpeg --no-install-recommends \
   && rm -rf /var/lib/apt/lists/*
# # # RUN apt-get update && apt-get install -y wget ffmpeg --no-install-recommends \
# # #     && wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh \
# # #     && bash /tmp/dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet \
# # #     && rm -rf /var/lib/apt/lists/*

# # # ENV PATH="/usr/share/dotnet:${PATH}"

# copy function
WORKDIR /home/site/wwwroot
#COPY . .
COPY --from=build /app/publish .

# # # RUN dotnet publish "BlobVideoTranscoder.csproj" -c Release -o /home/site/wwwroot/publish /p:GenerateFunctionMetadata=true

# # # # Replace working directory to published output
# # # WORKDIR /home/site/wwwroot/publish

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

# default entrypoint for dotnet isolated functions
#CMD ["dotnet", "BlobVideoTranscoder.dll"]
