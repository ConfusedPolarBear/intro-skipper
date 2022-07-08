# EDL support

The timestamps of discovered introductions can be written to [EDL](https://kodi.wiki/view/Edit_decision_list) files alongside your media files. EDL files are saved when:
* Scanning an episode for the first time, or
* If requested with the regenerate checkbox

## Configuration

Jellyfin must have read/write access to your TV show libraries in order to make use of this feature.

## Usage

To have the plugin create EDL files:
1. Change the EDL action from the default of None to any of the other supported EDL actions
2. Check the "Regenerate EDL files during next analysis" checkbox
   1. If this option is not selected, only seasons with a newly analyzed episode will have EDL files created.
