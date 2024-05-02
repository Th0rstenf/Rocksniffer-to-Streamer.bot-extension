# RockSniffer to Streamer.bot extension

A [streamer.bot](https://streamer.bot) implementation to replace Warths SceneSwitcher, and more.

## Description

## Switching Scenes
This code fetches the output of RockSniffer and evaluates game state and song timer. Depending on the state it switches to the scenes defined in global variables for Rocksmith, song, and break (pausing during a song).  

## Providing Global variables

The following data is written to global variables whenever they change:
* accuracy
* currentHitStreak
* highestHitStreak
* highestHitStreakSinceLaunch
* currentMissStreak
* totalNotes
* totalNotesHit
* totalNotesMissed
* totalNotesSinceLaunch
* totalNotesHitSinceLaunch
* totalNotesMissedSinceLaunch
* accuracySinceLaunch
* totalNotesLifeTime
* totalNotesHitLifeTime
* totalNotesMissedLifeTime
* accuracyLifeTime
* songLength (raw seconds)
* songLengthFormatted (Formatted as HH:MM:SS)

In addition to that, the following are provided to SB, as soon as the arrangement is identified:
* songName 
* artistName 
* albumName 
* arrangement 
* arrangementType
* tuning
* songLength (raw seconds)
* songLengthFormatted (HH:MM:SS)

## Reacting to Sections
Assuming the song has properly named sections, the following section types are recognized:

* Breakdown
* Bridge
* Chorus
* Riff
* Solo
* Verse
* No guitar
* Default (always active when scene name didn't give useful information)

For each of those sections, an enter and leave action is provided. Those will automatically be called by the SceneSwitcher action. Feel free to fill them with whatever you like.  
In addition to that, actions for entering/leaving a pause, the tuner and starting or ending a song are provided. 

## Guessing Game

The guessing game was introduced in version 0.3.0. When active, after start of a song the users have a configurable amount of time to guess your accuracy.
The game will only validate guesses if the amount of guesses is above a configurable threshold. The closest guess will be displayed in the chat.
In addition to that win counts for each user are tracked and can be fetched either as top ten list, or the rank for a specific user.

### Commands

* **!guess** - Guess the accuracy of the current song. Usage: `!guess 99.99`

(Introduced in release v0.3.0)

### Configurable Options

* **guessingIsActive** - Enable or disable the guessing game
* **MinGuesserCount  ** - Minimum number of valid guesses for the game to validate the round (default: 2) 
* **guessTimeOut** - Time in seconds to accept guesses(default: 30)
* **guessStartingText** - Text to display when the game starts
* **guessTimeOutText** - Text to display when no more guesses are accepted


## Installation and configuration

### Add to Streamer.bot
* Import the content of importCode.txt into streamer.bot
* Modify the global variables inside the *SceneSwitcher* action to match your configuration
* Check if the code is compiling. If it doesn't, a reference is missing. References necessary are:
    * mscorlib.dll
    * System.dll
    * System.Net.Http.dll
    * Newtonsoft.Json.dll (should be in your streamer.bot folder)
* Create a timed action (navigate Setting -> Timed Actions)
    * Configure an interval of 1 second
    * Make sure to tick *enabled* and *repeat*
    * Connect it with the imported action `SceneSwitcher`

### Adapt to your needs

Inside the SceneSwitcher action, there are several arguments that can/need to be changed:
For scene switching:
* menuScene - this should be the scene you want to load when you're in the menu or tuner
* songScenes - a comma separated list of scenes you could switch to during a song play (at start, the first is used)  
    * Song scenes can be switched automatically (see section scene switching)
    * The configured global songSwitchPeriod can be overriden for each scene individually.
    * It is possible to define a range, and have a randomized time each cycle  
    * Example: Scene1,Scene2#5-10,Scene3,Scene4#7
        * Scene1 would be active according to songSwitchPeriod
        * Scene2 would be active between 5 and 10 seconds, randomized each time
        * Scene3 would be active according to songSwitchPeriod
        * Scene4 would be active for 7 seconds

* pauseScene - the scene that will be loaded when pausing during a song

For the sniffer connection:
* snifferIP - ip address of the PC that is running RockSniffer. If it's the same it should be `"127.0.0.1"` (Quotes are not optional!)
* snifferPort - should usually never be touched (`9938`), but provided for sake of completeness

For determining when it is active:
* behavior - The following options are available:
  * Whitelist - Will only be active during the scenes defined in *menuScene*, *songScenes* and *pauseScene*
  * Blacklist - Will be active unless the current scene is in the blacklist
  * AlwaysOn - Self-explanatory
* blackList - a comma separated list of blacklisted scenes

Scene switching:
* switchScenes - True/False - enable or disable scene switching
* sceneSwitchPeriod - Automatic switching time between songScenes in seconds
* sceneSwitchCooldownPeriod - Cooldown time to wait after one scene change in seconds
* songSceneAutoSwitchMode - the mode of the automatic switching between the given song scenes after reached the songSwitchPeriod 
  * Off - automatic switching is disabled
  * Sequential - runs over the song scene list sequentially
  * Random - runs over the song scene list randomly

To enable/disable or set certain functions:
* sectionActions - True or False
 

## Dependencies

* Rocksmith
* [RockSniffer](https://github.com/kokolihapihvi/RockSniffer/releases)
* [streamer.bot](https://streamer.bot)

## Help

In case the switcher does not work, double check the spellings of the scenes.
Note that the IP address needs to be entered with quotes e.g.
```
"127.0.0.1"
```
Otherwise Streamer.bot will misinterpret it as double value. If the issues can not be solved this way, feel free to contact me in discord. See below.

With the usage of whitelist or blacklist, make sure to only switch to scenes that are valid for running. Otherwise you will not automatically switch back!

## Author

[Thorsten "Th0lamin" Fieger](https://discord.com/invite/m2fCKXn) 


## Version History
* 0.2
   * Detecting different types of sections and calling enter/leave actions in Streamer.bot 
   * Storing Note data and other meta data in global variables to be used in other actions
   * Behavior can now be changed between **Whitelist** / **Blacklist** / **Always on**
   * Added option to disable scene switches
   * Added option to disable section change actions
   * Providing note & meta data in global variables
* 0.1
    * Initial Release

## License

This project is licensed under the MIT License - see the LICENSE.txt file for details

## Acknowledgments

* [awesome-readme](https://github.com/matiassingers/awesome-readme)
* [Warths Scene Switcher](https://github.com/Warths/Rocksmith-Scene-Switcher)
