# Using the modified web interface

If you run Jellyfin as a container, there are two different ways to use the modified web interface:
1. Mounting the new web interface to your existing Jellyfin container, or
2. Switching container images

If you do not run Jellyfin as a container, you can follow the [native installation](native.md) instructions.

## Method 1: mounting the new web interface

1. Download the latest compiled version of the web interface from [GitHub actions](https://github.com/ConfusedPolarBear/intro-skipper/suites/6986965700/artifacts/273461104) (or compile [from source](https://github.com/ConfusedPolarBear/jellyfin-web/tree/intros))
   1. The GitHub actions download link will only work if you are signed in to GitHub.
2. Extract the archive somewhere on your server and make note of the full path to the `dist` folder
3. Mount the `dist` folder to your container as `/jellyfin/jellyfin-web`. Example docker-compose snippet:
```yaml
services:
    jellyfin:
        ports:
            - '8096:8096'
        volumes:
            - '/full/path/to/extracted/archive/dist:/jellyfin/jellyfin-web:ro' # <== add this line if using the official docker image
            - '/full/path/to/extracted/archive/dist:/usr/share/jellyfin/web # <== add this line if using the linuxserver docker image
            - '/config:/config'
            - '/media:/media:ro'
        image: 'jellyfin/jellyfin:10.8.0'
```

## Method 2: switching container images

1. Run the `ghcr.io/confusedpolarbear/jellyfin-intro-skipper` container just as you would any other Jellyfin container
    1. If you reuse the configuration data from another container, **make sure to create a backup first**.

The Dockerfile which builds this container can be viewed [here](../docker/Dockerfile).

