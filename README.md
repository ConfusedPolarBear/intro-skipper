# Intro Skipper (ALPHA)

Analyzes the audio of television episodes to detect and skip over intros. Currently in alpha.

## Requirements

* Jellyfin 10.8.0 beta 2 (or later)
* Modified [jellyfin-web](https://github.com/ConfusedPolarBear/jellyfin-web/tree/intros) interface with intro skip button
* [chromaprint](https://github.com/acoustid/chromaprint) (only v1.4.3 and later have been tested)

## Introduction requirements

Introductions will only detected if they are both:

* In the first 25% (or 10 minutes, whichever is smaller) of an episode
* 20 seconds or longer

## Native installation instructions

1. Install the `fpcalc` program
    1. On Debian based distributions, this is provided by the `libchromaprint-tools` package
    2. Compiled binaries can also be downloaded from the [GitHub repository](https://github.com/acoustid/chromaprint/releases/tag/v1.5.1)
2. Download the latest modified web interface from the releases tab and either:
    1. Serve the web interface directly from your Jellyfin server, or
    2. Serve the web interface using an external web server
    3. The corresponding source code can be found in this [fork](https://github.com/ConfusedPolarBear/jellyfin-web/tree/intros)
3. Add the plugin repository to your server: `https://raw.githubusercontent.com/ConfusedPolarBear/intro-skipper/master/manifest.json`
4. Install the Intro Skipper plugin and restart Jellyfin
5. Go to Dashboard -> Scheduled Tasks -> Analyze Episodes and click the play button
6. After the task completes, play some episodes and observe the results

## Docker container instructions

Coming soon.
