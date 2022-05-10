FROM jellyfin/jellyfin:10.8.0-beta2

RUN apt update && apt install -y libchromaprint-tools

COPY dist/ /jellyfin/jellyfin-web/
