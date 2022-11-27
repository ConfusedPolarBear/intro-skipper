# Using the modified web interface

If you run Jellyfin as a container, there are two different ways to use the modified web interface:
1. Mounting the new web interface to your existing Jellyfin container, or
2. Switching container images

If you do not run Jellyfin as a container, you can follow the [native installation](native.md) instructions.

## Method 1: mounting the new web interface

1. Download and extract the latest modified web interface from [GitHub actions](https://github.com/ConfusedPolarBear/intro-skipper/actions/workflows/container.yml)
    1. Click the most recent action run
    2. In the Artifacts section, click the `jellyfin-web-VERSION+COMMIT.tar.gz` link to download a pre-compiled copy of the web interface. This link will only work if you are signed into GitHub.
2. Extract the archive somewhere on your server and make note of the full path to the `dist` folder
3. Mount the `dist` folder to your container as `/jellyfin/jellyfin-web` if using the official container or /usr/share/jellyfin/web if using the linuxserver container . Example docker-compose snippet:
```yaml
services:
    jellyfin:
        ports:
            - '8096:8096'
        volumes:
            - '/full/path/to/extracted/dist:/jellyfin/jellyfin-web:ro'  # <== add this line if using the official container
            - '/full/path/to/extracted/dist:/usr/share/jellyfin/web:ro' # <== add this line if using the linuxserver container
            - '/config:/config'
            - '/media:/media:ro'
        image: 'jellyfin/jellyfin:10.8.0'
```

## Method 2: switching container images

1. Run the `ghcr.io/confusedpolarbear/jellyfin-intro-skipper` container just as you would any other Jellyfin container
    1. If you reuse the configuration data from another container, **make sure to create a backup first**.

The Dockerfile which builds this container can be viewed [here](../docker/Dockerfile).
