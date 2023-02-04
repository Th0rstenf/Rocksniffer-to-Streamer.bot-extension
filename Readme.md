# StreamerBot_RocksmithSceneSwitcher

A [streamer.bot](https://streamer.bot) implementation to replace Warth's SceneSwitcher

## Description

This code fetches the output of Rocksniffer and evaluates game state and song timer. Depending on the state it switches to the scenes defined in global variables for Rocksmith, song, and break (pausing during a song)
In addition to that, it checks the current sections name, and differentiates between:

* Breakdown
* Bridge
* Chorus
* Riff
* Solo
* Verse
* No guitar
* Default (always active when scene name didn't give useful information)

For each of those sections, an enter and leave action is provided. Those will automatically be called by the SceneSwitcher action. Feel free to fill them with whatever you like.

### Installing

* Import the content of import.txt into streamer.bot
* Modify the global variables inside the *SceneSwitcher* action to match your configuration
* Check if the code is compiling. If it doesn't, a reference is missing. References necessary are:
    * mscorlib.dll
    * System.dll
    * System.Net.Http.dll
    * Newtonsoft.Json.dll (should be in your streamer.bot folder)
* Create a timed action (navigate Setting -> Timed Actions)
* Connect it with the imported action `SceneSwitcher`



### Dependencies

* Rocksmith
* [Rocksniffer](https://github.com/kokolihapihvi/RockSniffer/releases)
* [streamer.bot](https://streamer.bot)

## Help

In case the switcher does not work, double check the spellings of the scenes.
Note that the IP address needs to be entered with quotes e.g.
```
"127.0.0.1"
```
Otherwise streamerbot will misinterpret it as double value. If the issues can not be solved this way, feel free to contact me in discord. See below.

## Author

[Thorsten "Th0lamin" Fieger](https://discord.com/invite/m2fCKXn) 


## Version History
* 0.2
   * Detecting different tyes of sections and calling enter/leave actions in Streamerbot 
   * Storing Note data and other meta data in global variables to be used in other actions
* 0.1
    * Initial Release

## License

This project is licensed under the MIT License - see the LICENSE.txt file for details

## Acknowledgments

* [awesome-readme](https://github.com/matiassingers/awesome-readme)
* [Warths Scene Switcher](https://github.com/Warths/Rocksmith-Scene-Switcher)

