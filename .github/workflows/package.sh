#!/bin/bash

# Check argument count
if [[ $# -ne 1 ]]; then
    echo "Usage: $0 VERSION"
    exit 1
fi

# Use provided tag to derive archive filename and short tag
version="$1"
zip="intro-skipper-$version.zip"
short="$(echo "$version" | sed "s/^v//")"

# Get the assembly version
CSPROJ="ConfusedPolarBear.Plugin.IntroSkipper/ConfusedPolarBear.Plugin.IntroSkipper.csproj"
assemblyVersion="$(grep -m1 -oE "([0-9]\.){3}[0-9]" "$CSPROJ")"

# Get the date
date="$(date --utc -Iseconds | sed "s/\+00:00/Z/")"

# Debug
echo "Version: $version ($short)"
echo "Archive: $zip"
echo

echo "Running unit tests"
dotnet test -p:DefineConstants=SKIP_FFMPEG_TESTS || exit 1
echo

echo "Building plugin in Release mode"
dotnet build -c Release || exit 1
echo

# Create packaging directory
mkdir package
cd package || exit 1

# Copy the freshly built plugin DLL to the packaging directory and archive
cp "../ConfusedPolarBear.Plugin.IntroSkipper/bin/Release/net6.0/ConfusedPolarBear.Plugin.IntroSkipper.dll" ./ || exit 1
zip "$zip" ConfusedPolarBear.Plugin.IntroSkipper.dll || exit 1

# Calculate the checksum of the archive
checksum="$(md5sum "$zip" | cut -f 1 -d " ")"

# Generate the manifest entry for this plugin
cat > manifest.json <<'EOF'
{
    "version": "ASSEMBLY",
    "changelog": "- See the full changelog at [GitHub](https://github.com/ConfusedPolarBear/intro-skipper/blob/master/CHANGELOG.md)\n",
    "targetAbi": "10.8.1.0",
    "sourceUrl": "https://github.com/ConfusedPolarBear/intro-skipper/releases/download/VERSION/ZIP",
    "checksum": "CHECKSUM",
    "timestamp": "DATE"
}
EOF

sed -i "s/ASSEMBLY/$assemblyVersion/" manifest.json
sed -i "s/VERSION/$version/" manifest.json
sed -i "s/ZIP/$zip/" manifest.json
sed -i "s/CHECKSUM/$checksum/" manifest.json
sed -i "s/DATE/$date/" manifest.json
