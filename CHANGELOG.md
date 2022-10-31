# Changelog

## v0.1.8.0 (no eta)
* New features
  * Detect ending credits in television episodes
* Internal changes
  * Move Chromaprint analysis code out of the episode analysis task
  * Add support for multiple analysis techinques

## v0.1.7.0 (2022-10-26)
* New features
  * Rewrote fingerprint comparison algorithm to be faster (~30x speedup) and detect more introductions
  * Detect silence at the end of introductions and use it to avoid skipping over the beginning of an episode
    * If you are upgrading from a previous release and want to use the silence detection feature on shows that have already been analyzed, you must click the `Erase introduction timestamps` button at the bottom of the plugin settings page
  * Add support bundle
  * Add maximum introduction duration
  * Support playing a few seconds from the end of the introduction to verify that no episode content was skipped over
    * Amount played is customizable and defaults to 2 seconds
  * Support modifying introduction detection algorithm settings
  * Add option to not skip the introduction in the first episode of a season
  * Add option to analyze show extras (specials)
* Fixes
  * Fix scheduled task interval (#79)
  * Prevent show names from becoming duplicated in the show name dropdown under the advanced section
  * Prevent virtual episodes from being inserted into the analysis queue

## v0.1.6.0 (2022-08-04)
* New features
  * Generate EDL files with intro timestamps ([documentation](docs/edl.md)) (#21)
  * Support selecting which libraries are analyzed (#37)
  * Support customizing [introduction requirements](README.md#introduction-requirements) (#38, #51)
    * Changing these settings will increase episode analysis times
  * Support adding and editing intro timestamps (#26)
  * Report how CPU time is being spent while analyzing episodes
    * CPU time reports can be viewed under "Analysis Statistics (experimental)" in the plugin configuration page
  * Sped up fingerprint analysis (not including fingerprint generation time) by 40%
  * Support erasing discovered introductions by season
  * Suggest potential shifts in the fingerprint visualizer

* Fixes
  * Ensure episode analysis queue matches the current filesystem and library state (#42, #60)
    * Fixes a bug where renamed or deleted episodes were being analyzed
  * Fix automatic intro skipping on Android TV (#57, #61)
  * Restore per season status updates in the log
  * Prevent null key in `/Intros/Shows` endpoint (#27)
  * Fix positioning of skip intro button on mobile devices (#43)
  * Ensure video playback always resumes after clicking the skip intro button (#44)

## v0.1.5.0 (2022-06-17)
* Use `ffmpeg` to generate audio fingerprints instead of `fpcalc`
  * Requires that the installed version of `ffmpeg`:
    * Was compiled with the `--enable-chromaprint` option
    * Understands the `-fp_format raw` flag
  * `jellyfin-ffmpeg 5.0.1-5` meets both of these requirements
* Version API endpoints
  * See [api.md](docs/api.md) for detailed documentation on how clients can work with this plugin
* Add commit hash to unstable builds
* Log media paths that are unable to be fingerprinted
* Report failure to the UI if the episode analysis queue is empty
* Allow customizing degrees of parallelism
  * Warning: Using a value that is too high will result in system instability
* Remove restart requirement to change auto skip setting
* Rewrite startup enqueue
* Fix deadlock issue on Windows (#23 by @nyanmisaka)
* Improve skip intro button styling & positioning (ConfusedPolarBear/jellyfin-web#91 by @Fallenbagel)
* Order episodes by `IndexNumber` (#25 reported by @Flo56958)


## v0.1.0.0 (2022-06-09)
* Add option to automatically skip intros
* Cache audio fingerprints by default
* Add fingerprint visualizer
* Add button to erase all previously discovered intro timestamps
* Made saving settings more reliable
* Switch to new fingerprint comparison algorithm
  * If you would like to test the new comparison algorithm, you will have to erase all previously discovered introduction timestamps.

## v0.0.0.3 (2022-05-21)
* Fix `fpcalc` version check

## v0.0.0.2 (2022-05-21)
* Analyze multiple seasons in parallel
* Reanalyze episodes with an unusually short or long intro sequence
* Check installed `fpcalc` version
* Clarify installation instructions

## v0.0.0.1 (2022-05-10)
* First alpha build
