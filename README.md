# Intro Skipper (beta)

<div align="center">
<img alt="Plugin Banner" src="https://raw.githubusercontent.com/ConfusedPolarBear/intro-skipper/master/images/logo.png" />
</div>

Analyzes the audio of television episodes to detect and skip over intros. Jellyfin must use a version of `ffmpeg` that has been compiled with `--enable-chromaprint` (`jellyfin-ffmpeg` versions 5.0.1-5 and later will work).

If you use the custom web interface on your server, you will be able to click a skip button to skip intros, like this:

![Skip intro button](images/skip-button.png)

However, if you use Jellyfin clients that do not use the web interface provided by the server, the plugin can be configured to automatically skip intros.

## Introduction requirements

Show introductions will only be detected if they are:

* Located within the first 25% of an episode, or the first 10 minutes, whichever is smaller
* At least 20 seconds long

## Step 1: Install the modified web interface (optional)
This step is only necessary if you do not use the automatic skip feature.
1. Run the `ghcr.io/confusedpolarbear/jellyfin-intro-skipper` container just as you would any other Jellyfin container
    1. If you reuse the configuration data from another container, **make sure to create a backup first**.
2. Follow the plugin installation steps below

## Step 2: Install the plugin
1. Add this plugin repository to your server: `https://raw.githubusercontent.com/ConfusedPolarBear/intro-skipper/master/manifest.json`
2. Install the Intro Skipper plugin from the General section
3. Restart Jellyfin
4. Go to Dashboard -> Scheduled Tasks -> Analyze Episodes and click the play button
5. After a season has completed analyzing, play some episodes from it and observe the results
    1. Status updates are logged before analyzing each season of a show

## Containerless installation
If you do not run Jellyfin as a container, you will need to follow the [native installation](docs/native.md) instructions.
