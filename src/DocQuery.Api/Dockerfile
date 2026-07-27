# Build context is the repo root (see docker-compose.yml) so the Api's
# project references to Core and the Providers resolve.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer: csproj files only, so dependency restore is cached until
# the project files themselves change.
COPY src/DocQuery.Core/DocQuery.Core.csproj DocQuery.Core/
COPY src/DocQuery.Providers.Local/DocQuery.Providers.Local.csproj DocQuery.Providers.Local/
COPY src/DocQuery.Providers.Azure/DocQuery.Providers.Azure.csproj DocQuery.Providers.Azure/
COPY src/DocQuery.Api/DocQuery.Api.csproj DocQuery.Api/
RUN dotnet restore DocQuery.Api/DocQuery.Api.csproj

COPY src/ .
RUN dotnet publish DocQuery.Api/DocQuery.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Curated demo corpus, seeded at startup when Demo:Enabled=true (SeedPath
# should point at /app/seed). Files are listed explicitly — the corpus is a
# deliberate choice, never "whatever is in the folder".
COPY docs/samples/certifications.md \
     docs/samples/open-source-contributions.md \
     docs/samples/project-docquery.md \
     docs/samples/resume.md \
     /app/seed/

# All DocQuery config (provider, Ollama/Chroma URLs) arrives via environment
# variables from compose — appsettings.json is .dockerignored so secrets can
# never be baked into an image.
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "DocQuery.Api.dll"]
