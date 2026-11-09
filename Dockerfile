FROM node:24-bookworm-slim AS frontend
WORKDIR /frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS backend
WORKDIR /source
COPY Directory.Build.props ./
COPY src/ ./src/
RUN dotnet publish src/Host/Host.csproj -c Release -o /out --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
USER root
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg curl ca-certificates && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/assessment/recordings /var/lib/assessment/keys \
    && chown -R app:app /var/lib/assessment
WORKDIR /app
COPY --from=backend /out/ ./
COPY --from=frontend /frontend/dist/ ./wwwroot/
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Host.dll"]
