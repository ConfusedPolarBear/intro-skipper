# Release procedure

## Run tests

1. Run unit tests with `dotnet test`
2. Run end to end tests with `JELLYFIN_TOKEN=api_key_here python3 main.py`

## Release plugin

1. Update and commit latest changelog and manifest
2. Tag latest commit **without pushing**
3. Build release DLL with `dotnet build -c Release`
4. Zip release DLL
5. Test plugin manifest
   1. Replace manifest URL with local IP address
   2. Serve release ZIP and manifest with `python3 -m http.server`
   3. Test updating plugin
6. Push tag
7. Create release on GitHub with the following files:
   1. Archived plugin DLL
   2. Latest web interface

## Release container

1. Run publish container action
