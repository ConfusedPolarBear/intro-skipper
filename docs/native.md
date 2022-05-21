# Native installation

## Requirements

* Jellyfin 10.8.0 beta 2 (beta 3 may also work, untested)
* Compiled [jellyfin-web](https://github.com/ConfusedPolarBear/jellyfin-web/tree/intros) interface with intro skip button
* [chromaprint](https://github.com/acoustid/chromaprint) (only versions 1.4.3 and later have been verified to work)

## Instructions

1. Install the `fpcalc` program
    1. On Debian based distributions, this is provided by the `libchromaprint-tools` package
    2. Compiled binaries can also be downloaded from the [GitHub repository](https://github.com/acoustid/chromaprint/releases/tag/v1.5.1)
2. Download the latest modified web interface from the releases tab and either:
    1. Serve the web interface directly from your Jellyfin server, or
    2. Serve the web interface using an external web server
    3. The corresponding source code can be found in this [fork](https://github.com/ConfusedPolarBear/jellyfin-web/tree/intros)
3. Follow the plugin installation steps from the readme
