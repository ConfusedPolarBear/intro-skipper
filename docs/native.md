# Native installation

## Requirements

* Jellyfin 10.8.0
* Compiled [jellyfin-web](https://github.com/ConfusedPolarBear/jellyfin-web/tree/intros) interface with intro skip button

## Instructions

1. Download and extract the latest modified web interface from [GitHub actions](https://github.com/ConfusedPolarBear/intro-skipper/actions/workflows/container.yml)
    1. Click the most recent action run
    2. In the Artifacts section, click the `jellyfin-web-VERSION+COMMIT.tar.gz` link to download a pre-compiled copy of the web interface. This link will only work if you are signed into GitHub.
2. Make a backup of the original web interface
    1. On Linux, the web interface is located in `/usr/share/jellyfin/web/`
    2. On Windows, the web interface is located in `C:\Program Files\Jellyfin\Server\jellyfin-web`
3. Copy the contents of the `dist` folder you downloaded in step 1 into Jellyfin's web folder
4. Follow the plugin installation steps from the readme
