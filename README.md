# Intro Skipper (ALPHA)

Analyzes the audio of television episodes to detect and skip over intros. Currently in alpha.

The custom web interface **is required** in order to display the skip intro button inside the video player.

## Introduction requirements

Show introductions will only be detected if they are:

* Located within the first 25% of an episode, or the first 10 minutes, whichever is smaller
* At least 20 seconds long

## Container installation

1. Run the `ghcr.io/confusedpolarbear/jellyfin-intro-skipper` container just as you would any other Jellyfin container
    1. If you reuse the configuration data from another container, **make sure to create a backup first**.
2. Follow the plugin installation steps below

## Plugin installation
1. Add this plugin repository to your server: `https://raw.githubusercontent.com/ConfusedPolarBear/intro-skipper/master/manifest.json`
2. Install the Intro Skipper plugin from the General section
3. Restart Jellyfin
4. Go to Dashboard -> Scheduled Tasks -> Analyze Episodes and click the play button
5. After a season has completed analyzing, play some episodes from it and observe the results
    1. Status updates are logged before analyzing each season of a show

## Native installation
### Requirements

* Jellyfin 10.8.0 beta 2 (or later)
* Compiled [jellyfin-web](https://github.com/ConfusedPolarBear/jellyfin-web/tree/intros) interface with intro skip button
* [chromaprint](https://github.com/acoustid/chromaprint) (only versions 1.4.3 and later have been verified to work)

### Instructions

1. Install the `fpcalc` program
    1. On Debian based distributions, this is provided by the `libchromaprint-tools` package
    2. Compiled binaries can also be downloaded from the [GitHub repository](https://github.com/acoustid/chromaprint/releases/tag/v1.5.1)
2. Download the latest modified web interface from the releases tab and either:
    1. Serve the web interface directly from your Jellyfin server, or
    2. Serve the web interface using an external web server
    3. The corresponding source code can be found in this [fork](https://github.com/ConfusedPolarBear/jellyfin-web/tree/intros)
3. Follow the plugin installation steps above
