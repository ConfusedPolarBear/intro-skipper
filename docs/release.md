# Release procedure

## Run tests

1. Run unit tests with `dotnet test`
2. Run end to end tests with `JELLYFIN_TOKEN=api_key_here python3 main.py`

## Release plugin

1. Run package plugin action and download bundle
2. Combine generated `manifest.json` with main plugin manifest
3. Test plugin manifest
   1. Replace manifest URL with local IP address
   2. Serve release ZIP and manifest with `python3 -m http.server`
   3. Test updating plugin
4. Create release on GitHub with the following files:
   1. Archived plugin DLL
   2. Link to the latest web interface

## Release container

1. Run publish container action
2. Update `latest` tag
    1. `docker tag ghcr.io/confusedpolarbear/jellyfin-intro-skipper:{COMMIT,latest}`
    2. `docker push ghcr.io/confusedpolarbear/jellyfin-intro-skipper:latest`
