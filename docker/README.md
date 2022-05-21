Build instructions for the `ghcr.io/confusedpolarbear/jellyfin-intro-skipper` container:

1. Clone `https://github.com/ConfusedPolarBear/jellyfin-web` and checkout the `intros` branch
2. Run `npm run build:production`
3. Copy the `dist` folder into this folder
4. Run `docker build .`
