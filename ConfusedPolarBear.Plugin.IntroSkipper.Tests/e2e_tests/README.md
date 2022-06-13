# End to end testing framework

This folder holds scripts used in performing end to end testing of the plugin. The script:

1. **Erases all currently discovered introduction timestamps**
2. Runs the Analyze episodes task
3. Waits for the analysis to complete
4. Checks that the current results are within one second of a previous result

## Usage

1. Save the response returned by `/Intros/All?api_key=KEY` to a file somewhere.
2. Set the environment variable `JELLYFIN_TOKEN` to the access token of an administrator.
3. Run `python3 main.py -f FILENAME`
