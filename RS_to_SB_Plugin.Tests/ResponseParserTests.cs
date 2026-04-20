using System.Collections.Generic;
using Newtonsoft.Json;
using Xunit;

/// <summary>
/// Tests for CPHInline.ResponseParser game stage evaluation and section identification.
/// </summary>
public class ResponseParserTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a fully initialised ResponseParser with scene switching enabled
    /// and section actions disabled (the common baseline for most tests).
    /// </summary>
    private static (CPHInline.ResponseParser parser, TestCPH cph) CreateParser(
        string menuScene   = "MenuScene",
        string songScenes  = "SongScene",
        string pauseScene  = "PauseScene",
        bool   switchScenes = true)
    {
        var cph = new TestCPH { CurrentScene = menuScene };
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesLifeTime]     = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesHitLifeTime]   = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesMissedLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameAccuracyLifeTime]        = "0";
        cph.GlobalVars[Constants.GlobalVarNameGuessingWinnersCount]    = "{}";
        // ReadArgumentAsStringArray uses CPH.TryGetArg, so the value must also be in GlobalVars
        cph.GlobalVars[Constants.ArgumentNameSongScenes] = songScenes;

        var args = new Dictionary<string, object>
        {
            [Constants.ArgumentNameMenuScene]                = menuScene,
            [Constants.ArgumentNameSongScenes]               = songScenes,
            [Constants.ArgumentNamePauseScene]               = pauseScene,
            [Constants.ArgumentNameSwitchScenes]             = switchScenes.ToString(), // Streamer.bot passes all args as strings
            [Constants.ArgumentNameSongSceneAutoSwitchMode]  = "Off",
            [Constants.ArgumentNameSectionActions]           = "False",
            [Constants.ArgumentNameBehavior]                 = "AlwaysOn",
            [Constants.ArgumentNameSceneSwitchPeriod]        = "5",
            [Constants.ArgumentNameSceneSwitchCooldownPeriod] = "0",
            [Constants.GlobalVarNameGuessingIsActive]        = "False",
            [Constants.GlobalVarNameGuessingMinGuesser]      = "2",
            [Constants.GlobalVarNameGuessingGuessTime]       = "30",
            [Constants.ArgumentNameGuessingStartedText]      = "Guessing started",
            [Constants.ArgumentNameGuessingTimeoutText]      = "Guessing ended",
        };

        return BuildParser(cph, args, startingScene: menuScene);
    }

    /// <summary>
    /// Creates a fully initialised ResponseParser with section actions enabled,
    /// starting inside a song scene.
    /// </summary>
    private static (CPHInline.ResponseParser parser, TestCPH cph) CreateParserWithSectionActionsEnabled(
        string songScene = "SongScene")
    {
        var cph = new TestCPH { CurrentScene = songScene };
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesLifeTime]     = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesHitLifeTime]   = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesMissedLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameAccuracyLifeTime]        = "0";
        cph.GlobalVars[Constants.GlobalVarNameGuessingWinnersCount]    = "{}";
        // ReadArgumentAsStringArray uses CPH.TryGetArg, so the value must also be in GlobalVars
        cph.GlobalVars[Constants.ArgumentNameSongScenes] = songScene;

        var args = new Dictionary<string, object>
        {
            [Constants.ArgumentNameMenuScene]                = "MenuScene",
            [Constants.ArgumentNameSongScenes]               = songScene,
            [Constants.ArgumentNamePauseScene]               = "PauseScene",
            [Constants.ArgumentNameSwitchScenes]             = "True",  // Streamer.bot passes all args as strings
            [Constants.ArgumentNameSongSceneAutoSwitchMode]  = "Off",
            [Constants.ArgumentNameSectionActions]           = "True",  // Streamer.bot passes all args as strings
            [Constants.ArgumentNameBehavior]                 = "AlwaysOn",
            [Constants.ArgumentNameSceneSwitchPeriod]        = "5",
            [Constants.ArgumentNameSceneSwitchCooldownPeriod] = "0",
            [Constants.GlobalVarNameGuessingIsActive]        = "False",
            [Constants.GlobalVarNameGuessingMinGuesser]      = "2",
            [Constants.GlobalVarNameGuessingGuessTime]       = "30",
            [Constants.ArgumentNameGuessingStartedText]      = "Guessing started",
            [Constants.ArgumentNameGuessingTimeoutText]      = "Guessing ended",
        };

        return BuildParser(cph, args, startingScene: songScene);
    }

    /// <summary>
    /// Shared wiring used by both factory methods above.
    /// </summary>
    private static (CPHInline.ResponseParser parser, TestCPH cph) BuildParser(
        TestCPH cph, Dictionary<string, object> args, string startingScene)
    {
        var dataHandler    = new CPHInline.DataHandler(cph, args);
        var sceneController = new CPHInline.SceneController(cph);
        sceneController.SetCooldownPeriod(0);
        // Querying the current scene initialises the internal OBS stream-app reference
        _ = sceneController.GetCurrentScene();
        var guessingGame = new CPHInline.GuessingGame(cph, dataHandler);
        var parser = new CPHInline.ResponseParser(cph, sceneController, guessingGame, dataHandler);
        parser.Init();
        parser.UpdateConfig();
        parser.SetCurrentScene(startingScene);

        return (parser, cph);
    }

    /// <summary>
    /// Builds a minimal Response object for the given game stage and timer value.
    /// </summary>
    private static Response BuildResponse(string gameStage, double songTimer = 0.0)
    {
        return new Response
        {
            MemoryReadout = new MemoryReadout
            {
                SongId        = "song1",
                ArrangementId = "arr1",
                GameStage     = gameStage,
                SongTimer     = songTimer,
                NoteData      = new NoteData
                {
                    Accuracy         = 0.0,
                    TotalNotes       = 0,
                    TotalNotesHit    = 0,
                    CurrentHitStreak = 0,
                    HighestHitStreak = 0,
                    TotalNotesMissed = 0,
                    CurrentMissStreak = 0
                }
            },
            SongDetails = new SongDetails
            {
                SongName     = "Test Song",
                ArtistName   = "Test Artist",
                SongLength   = 120.0,
                AlbumName    = "Test Album",
                AlbumYear    = 2020,
                Arrangements = new Arrangement[0]
            }
        };
    }

    /// <summary>
    /// Builds a Response that contains a single arrangement with one section.
    /// </summary>
    private static Response BuildResponseWithSection(string sectionName, double songTimer = 25.0)
    {
        return new Response
        {
            MemoryReadout = new MemoryReadout
            {
                SongId        = "song1",
                ArrangementId = "arr1",
                GameStage     = "las_game",
                SongTimer     = songTimer,
                NoteData      = new NoteData()
            },
            SongDetails = new SongDetails
            {
                SongName   = "Test",
                ArtistName = "Artist",
                SongLength = 120.0,
                AlbumName  = "Album",
                AlbumYear  = 2020,
                Arrangements = new[]
                {
                    new Arrangement
                    {
                        Name          = "Lead",
                        ArrangementID = "arr1",
                        type          = "Lead",
                        Tuning        = new Tuning { TuningName = "E Standard" },
                        Sections      = new[] { new Section { Name = sectionName, StartTime = 0.0, EndTime = 60.0 } }
                    }
                }
            }
        };
    }

    // ---------------------------------------------------------------------------
    // Timer tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetCurrentTimer_AfterUpdateStageAndTimer_ReturnsSongTimerFromResponse()
    {
        // Given a parser and an in-song response with a song timer of 42.5 s
        var (parser, _) = CreateParser();
        var response = BuildResponse("las_game", songTimer: 42.5);
        parser.SetResponse(response);

        // When the stage and timer are updated from the response
        parser.UpdateStageAndTimer();

        // Then the parser exposes that exact timer value
        Assert.Equal(42.5, parser.GetCurrentTimer());
    }

    [Theory]
    [InlineData("las_game")]
    [InlineData("sa_game")]
    public void UpdateStageAndTimer_InSongGameStages_SetsTimerCorrectly(string gameStage)
    {
        // Given a response for a recognised in-song game stage (las_game / sa_game) with a timer of 30 s
        var (parser, _) = CreateParser();
        var response = BuildResponse(gameStage, songTimer: 30.0);
        parser.SetResponse(response);

        // When the stage and timer are updated
        parser.UpdateStageAndTimer();

        // Then the song timer is stored correctly regardless of which in-song stage token was used
        Assert.Equal(30.0, parser.GetCurrentTimer());
    }

    [Fact]
    public void UpdateStageAndTimer_WithNullMemoryReadout_ThrowsException()
    {
        // Given a response whose MemoryReadout is null (sniffer data unavailable)
        var (parser, _) = CreateParser();
        var response = new Response { MemoryReadout = null!, SongDetails = null! };
        parser.SetResponse(response);

        // When UpdateStageAndTimer is called
        // Then a descriptive Exception is thrown explaining that sniffer data could not be read
        Assert.Throws<Exception>(() => parser.UpdateStageAndTimer());
    }

    // ---------------------------------------------------------------------------
    // Arrangement identification tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void IdentifyArrangement_WhenArrangementIdMatches_ReturnsTrue()
    {
        // Given a response where the active ArrangementId matches one of the song's arrangements
        var (parser, _) = CreateParser();
        var response = new Response
        {
            MemoryReadout = new MemoryReadout
            {
                SongId        = "song1",
                ArrangementId = "arr1",
                GameStage     = "las_game",
                SongTimer     = 10.0,
                NoteData      = new NoteData()
            },
            SongDetails = new SongDetails
            {
                SongName   = "Test Song",
                ArtistName = "Test Artist",
                SongLength = 120.0,
                AlbumName  = "Test Album",
                AlbumYear  = 2020,
                Arrangements = new[]
                {
                    new Arrangement
                    {
                        Name          = "Lead",
                        ArrangementID = "arr1",
                        type          = "Lead",
                        Tuning        = new Tuning { TuningName = "E Standard" },
                        Sections      = new[] { new Section { Name = "intro", StartTime = 0.0, EndTime = 30.0 } }
                    }
                }
            }
        };
        parser.SetResponse(response);

        // When arrangement identification is performed
        var identified = parser.IdentifyArrangement();

        // Then the matching arrangement is found and true is returned
        Assert.True(identified);
    }

    [Fact]
    public void IdentifyArrangement_WhenArrangementIdDoesNotMatch_ReturnsFalse()
    {
        // Given a response where the active ArrangementId does not match any arrangement in the song
        var (parser, _) = CreateParser();
        var response = new Response
        {
            MemoryReadout = new MemoryReadout
            {
                SongId        = "song1",
                ArrangementId = "nonexistent",
                GameStage     = "las_game",
                SongTimer     = 10.0,
                NoteData      = new NoteData()
            },
            SongDetails = new SongDetails
            {
                SongName   = "Test Song",
                ArtistName = "Test Artist",
                SongLength = 120.0,
                AlbumName  = "Test Album",
                AlbumYear  = 2020,
                Arrangements = new[]
                {
                    new Arrangement
                    {
                        Name          = "Lead",
                        ArrangementID = "arr1",
                        type          = "Lead",
                        Tuning        = new Tuning { TuningName = "E Standard" },
                        Sections      = new Section[0]
                    }
                }
            }
        };
        parser.SetResponse(response);

        // When arrangement identification is performed
        var identified = parser.IdentifyArrangement();

        // Then false is returned because no arrangement ID matches
        Assert.False(identified);
    }

    // ---------------------------------------------------------------------------
    // Scene switching tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void PerformSceneSwitchIfNecessary_FromMenuToSong_SwitchesToSongScene()
    {
        // Given a parser configured with "MenuScene" and "SongScene", currently on "MenuScene"
        var (parser, cph) = CreateParser(menuScene: "MenuScene", songScenes: "SongScene");
        cph.CurrentScene = "MenuScene";
        parser.SetCurrentScene("MenuScene");

        // When the game transitions from the main menu to an active song
        var menuResponse = BuildResponse("MainMenu", 0.0);
        parser.SetResponse(menuResponse);
        parser.UpdateStageAndTimer();
        parser.PerformSceneSwitchIfNecessary();

        var songResponse = BuildResponse("las_game", 1.0);
        parser.SetResponse(songResponse);
        parser.UpdateStageAndTimer();
        parser.PerformSceneSwitchIfNecessary();

        // Then OBS is switched to the designated song scene
        Assert.Equal("SongScene", cph.CurrentScene);
    }

    [Fact]
    public void PerformSceneSwitchIfNecessary_InSongToMenu_SwitchesToMenuScene()
    {
        // Given a parser that has been tracking an active song on "SongScene"
        var (parser, cph) = CreateParser(menuScene: "MenuScene", songScenes: "SongScene");
        cph.CurrentScene = "SongScene";
        parser.SetCurrentScene("SongScene");

        var songResponse = BuildResponse("las_game", 30.0);
        parser.SetResponse(songResponse);
        parser.UpdateStageAndTimer();
        parser.PerformSceneSwitchIfNecessary();

        // When the game returns to the main menu
        var menuResponse = BuildResponse("MainMenu", 0.0);
        parser.SetResponse(menuResponse);
        parser.UpdateStageAndTimer();
        parser.PerformSceneSwitchIfNecessary();

        // Then OBS is switched back to the menu scene
        Assert.Equal("MenuScene", cph.CurrentScene);
    }

    // ---------------------------------------------------------------------------
    // Section identification tests
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("solo_1",    "Solo")]
    [InlineData("riff_A",    "Riff")]
    [InlineData("noguitar",  "NoGuitar")]
    [InlineData("bridge_2",  "Bridge")]
    [InlineData("breakdown", "Breakdown")]
    [InlineData("chorus_A",  "Chorus")]
    [InlineData("verse_1",   "Verse")]
    public void CheckSectionActions_SectionNameMappedCorrectly_TriggersCorrectEnterEvent(
        string sectionName, string expectedSectionTypeName)
    {
        // Given a parser with section actions enabled, currently in the song scene
        var (parser, cph) = CreateParserWithSectionActionsEnabled();

        // When the parser processes an in-song response whose first section has the given name
        var response = BuildResponseWithSection(sectionName);
        parser.SetResponse(response);
        parser.UpdateStageAndTimer();
        parser.IdentifyArrangement();
        parser.CheckSectionActions();

        // Then the correct "enter<SectionType>" code event is triggered
        var enterEvent = $"enter{expectedSectionTypeName}";
        Assert.Contains(cph.TriggeredEvents, e => e.EventName == enterEvent);
    }

    [Fact]
    public void CheckSectionActions_IntroSection_DoesNotTriggerEnterDefaultBecauseDefaultIsInitialState()
    {
        // Given a parser with section actions enabled, starting in the song scene
        // "intro" maps to SectionType.Default, which is also the initial lastSectionType,
        // so entering a Default section at song start produces no enter/leave events.
        var (parser, cph) = CreateParserWithSectionActionsEnabled();

        // When the parser processes a response whose first section is "intro" (→ Default)
        var response = BuildResponseWithSection("intro");
        parser.SetResponse(response);
        parser.UpdateStageAndTimer();
        parser.IdentifyArrangement();
        parser.CheckSectionActions();

        // Then no "enterDefault" event is triggered because Default is the initial section state
        Assert.DoesNotContain(cph.TriggeredEvents, e => e.EventName == "enterDefault");
    }
}
