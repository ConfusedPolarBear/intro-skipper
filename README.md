# Intro Skipper (beta)

<div align="center">
<img alt="Plugin Banner" src="https://raw.githubusercontent.com/ConfusedPolarBear/intro-skipper/master/images/logo.png" />
</div>

Analyzes the audio of television episodes to detect and skip over intros.

If you use the custom web interface on your server, you will be able to click a button to skip intros, like this:

![Skip intro button](images/skip-button.png)

However, if you want to use an unmodified installation of Jellyfin 10.8.0 or use clients that do not use the web interface provided by the server, the plugin can be configured to automatically skip intros.

Plugin versions older than v0.1.5.0 require `fpcalc` to be installed.

## Introduction requirements

Show introductions will only be detected if they are:

* Located within the first 25% of an episode, or the first 10 minutes, whichever is smaller
* At least 20 seconds long

## Step 1: Optional: install the modified web interface
While this plugin is fully compatible with an unmodified version of Jellyfin 10.8.0, using a modified web interface allows you to click a button to skip intros. If you skip this step and do not use the modified web interface, you will have to enable the "Automatically skip intros" option in the plugin settings.

1. Run the `ghcr.io/confusedpolarbear/jellyfin-intro-skipper` container just as you would any other Jellyfin container
    1. If you reuse the configuration data from another container, **make sure to create a backup first**.
2. Follow the plugin installation steps below

## Step 2: Install the plugin
1. Add this plugin repository to your server: `https://raw.githubusercontent.com/ConfusedPolarBear/intro-skipper/master/manifest.json`
2. Install the Intro Skipper plugin from the General section
3. Restart Jellyfin
4. If you did not install the modified web interface, enable automatic skipping
    1. Go to Dashboard -> Plugins -> Intro Skipper
    2. Check "Automatically skip intros" and click Save
5. Go to Dashboard -> Scheduled Tasks -> Analyze Episodes and click the play button
6. After a season has completed analyzing, play some episodes from it and observe the results
    1. Status updates are logged before analyzing each season of a show

## Containerless installation
If you do not run Jellyfin as a container and want to use the skip intro button, you will need to follow the [native installation](docs/native.md) instructions.

## Documentation

Documentation about how the API works can be found in [api.md](docs/api.md).
