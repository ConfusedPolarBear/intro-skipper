# API

## General

The main API endpoint exposed by this plugin is `/Episode/{ItemId}/IntroTimestamps`. If an introduction was detected inside of a television episode, this endpoint will return the timestamps of that intro.

An API version can be optionally selected by appending `/v{Version}` to the URL. If a version is not specified, version 1 will be selected.

## API version 1 (default)

API version 1 was introduced with the initial alpha release of the plugin. It is accessible (via a `GET` request) on the following URLs:
* `/Episode/{ItemId}/IntroTimestamps`
* `/Episode/{ItemId}/IntroTimestamps/v1`

Both of these endpoints require an authorization token to be provided.

The possible status codes of this endpoint are:
* `200 (OK)`: An introduction was detected for this item and the response is deserializable as JSON using the schema below.
* `404 (Not Found)`: Either no introduction was detected for this item or it is not a television episode.

JSON schema:

```jsonc
{
    "EpisodeId": "{item id}",   // Unique GUID for this item as provided by Jellyfin.
    "Valid": true,              // Used internally to mark items that have intros. Should be ignored as it will always be true.
    "IntroStart": 100.5,        // Start time (in seconds) of the introduction.
    "IntroEnd": 130.42,         // End time (in seconds) of the introduction.
    "ShowSkipPromptAt": 95.5,   // Recommended time to display an on-screen intro skip prompt to the user.
    "HideSkipPromptAt": 110.5   // Recommended time to hide the on-screen intro skip prompt.
}
```

The `ShowSkipPromptAt` and `HideSkipPromptAt` properties are derived from the start time of the introduction and are customizable by the user from the plugin's settings.

### Example curl command

`curl` command to get introduction timestamps for the item with id `12345678901234567890123456789012`:

```shell
curl http://127.0.0.1:8096/Episode/12345678901234567890123456789012/IntroTimestamps/v1 -H 'Authorization: MediaBrowser Token="98765432109876543210987654321098"'
```

This returns the following JSON object:
```json
{
  "EpisodeId": "12345678901234567890123456789012",
  "Valid": true,
  "IntroStart": 304,
  "IntroEnd": 397.48,
  "ShowSkipPromptAt": 299,
  "HideSkipPromptAt": 314
}
```
