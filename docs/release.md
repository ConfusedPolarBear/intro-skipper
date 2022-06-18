# Release procedure

## Run tests

1. Run unit tests with `dotnet test`
2. Run end to end tests with `JELLYFIN_TOKEN=api_key_here python3 main.py`

## Release plugin

1. Build release DLL with `dotnet build -c Release`
2. Zip release DLL
3. Update and commit latest changelog and manifest
4. Test plugin manifest
   1. Replace manifest URL with local IP address
   2. Serve release ZIP and manifest with `python3 -m http.server`
   3. Test updating plugin
5. Tag and push latest commit
6. Create release on GitHub with the following files:
   1. Archived plugin DLL
   2. Latest web interface

## Release container

1. Run publish container action
