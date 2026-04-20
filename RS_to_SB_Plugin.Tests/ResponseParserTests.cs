using System.Collections.Generic;
using Newtonsoft.Json;
using Xunit;

/// <summary>
/// Tests for CPHInline.ResponseParser game stage evaluation and section identification.
/// </summary>
public class ResponseParserTests
{
    private static (CPHInline.ResponseParser parser, TestCPH cph) CreateParser(
        string menuScene = "MenuScene",
        string songScenes = "SongScene",
        string pauseScene = "PauseScene",
        bool switchScenes = true)
    {
        var cph = new TestCPH { CurrentScene = menuScene };
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesHitLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesMissedLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameAccuracyLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameGuessingWinnersCount] = "{}";
        // ReadArgumentAsStringArray uses CPH.TryGetArg, so the value must also be in GlobalVars
        cph.GlobalVars[Constants.ArgumentNameSongScenes] = songScenes;

        var args = new Dictionary<string, object>
        {
            [Constants.ArgumentNameMenuScene] = menuScene,
            [Constants.ArgumentNameSongScenes] = songScenes,
            [Constants.ArgumentNamePauseScene] = pauseScene,
            [Constants.ArgumentNameSwitchScenes] = switchScenes.ToString(),
            [Constants.ArgumentNameSongSceneAutoSwitchMode] = "Off",
            [Constants.ArgumentNameSectionActions] = "False",
            [Constants.ArgumentNameBehavior] = "AlwaysOn",
            [Constants.ArgumentNameSceneSwitchPeriod] = "5",
            [Constants.ArgumentNameSceneSwitchCooldownPeriod] = "0",
            [Constants.GlobalVarNameGuessingIsActive] = "False",
            [Constants.GlobalVarNameGuessingMinGuesser] = "2",
            [Constants.GlobalVarNameGuessingGuessTime] = "30",
            [Constants.ArgumentNameGuessingStartedText] = "Guessing started",
            [Constants.ArgumentNameGuessingTimeoutText] = "Guessing ended",
        };

        var dataHandler = new CPHInline.DataHandler(cph, args);
        var sceneController = new CPHInline.SceneController(cph);
        sceneController.SetCooldownPeriod(0);
        // Initialize stream app by querying the current scene
        _ = sceneController.GetCurrentScene();
        var guessingGame = new CPHInline.GuessingGame(cph, dataHandler);
        var parser = new CPHInline.ResponseParser(cph, sceneController, guessingGame, dataHandler);
        parser.Init();
        parser.UpdateConfig();

        return (parser, cph);
    }

    private static Response BuildResponse(string gameStage, double songTimer = 0.0)
    {
        return new Response
        {
            MemoryReadout = new MemoryReadout
            {
                SongId = "song1",
                ArrangementId = "arr1",
                GameStage = gameStage,
                SongTimer = songTimer,
                NoteData = new NoteData
                {
                    Accuracy = 0.0,
                    TotalNotes = 0,
                    TotalNotesHit = 0,
                    CurrentHitStreak = 0,
                    HighestHitStreak = 0,
                    TotalNotesMissed = 0,
                    CurrentMissStreak = 0
                }
            },
            SongDetails = new SongDetails
            {
                SongName = "Test Song",
                ArtistName = "Test Artist",
                SongLength = 120.0,
                AlbumName = "Test Album",
                AlbumYear = 2020,
                Arrangements = new Arrangement[0]
            }
        };
    }

    [Fact]
    public void GetCurrentTimer_AfterUpdateStageAndTimer_ReturnsSongTimerFromResponse()
    {
        var (parser, _) = CreateParser();
        var response = BuildResponse("las_game", songTimer: 42.5);
        parser.SetResponse(response);

        parser.UpdateStageAndTimer();

        Assert.Equal(42.5, parser.GetCurrentTimer());
    }

    [Fact]
    public void UpdateStageAndTimer_WithNullMemoryReadout_ThrowsException()
    {
        var (parser, _) = CreateParser();
        var response = new Response
        {
            MemoryReadout = null!,
            SongDetails = null!
        };
        parser.SetResponse(response);

        Assert.Throws<Exception>(() => parser.UpdateStageAndTimer());
    }

    [Theory]
    [InlineData("las_game")]
    [InlineData("sa_game")]
    public void UpdateStageAndTimer_InSongGameStages_SetsTimerCorrectly(string gameStage)
    {
        var (parser, _) = CreateParser();
        var response = BuildResponse(gameStage, songTimer: 30.0);
        parser.SetResponse(response);

        parser.UpdateStageAndTimer();

        Assert.Equal(30.0, parser.GetCurrentTimer());
    }

    [Fact]
    public void IdentifyArrangement_WhenArrangementIdMatches_ReturnsTrue()
    {
        var (parser, _) = CreateParser();
        var response = new Response
        {
            MemoryReadout = new MemoryReadout
            {
                SongId = "song1",
                ArrangementId = "arr1",
                GameStage = "las_game",
                SongTimer = 10.0,
                NoteData = new NoteData()
            },
            SongDetails = new SongDetails
            {
                SongName = "Test Song",
                ArtistName = "Test Artist",
                SongLength = 120.0,
                AlbumName = "Test Album",
                AlbumYear = 2020,
                Arrangements = new[]
                {
                    new Arrangement
                    {
                        Name = "Lead",
                        ArrangementID = "arr1",
                        type = "Lead",
                        Tuning = new Tuning { TuningName = "E Standard" },
                        Sections = new[]
                        {
                            new Section { Name = "intro", StartTime = 0.0, EndTime = 30.0 }
                        }
                    }
                }
            }
        };
        parser.SetResponse(response);

        var identified = parser.IdentifyArrangement();

        Assert.True(identified);
    }

    [Fact]
    public void IdentifyArrangement_WhenArrangementIdDoesNotMatch_ReturnsFalse()
    {
        var (parser, _) = CreateParser();
        var response = new Response
        {
            MemoryReadout = new MemoryReadout
            {
                SongId = "song1",
                ArrangementId = "nonexistent",
                GameStage = "las_game",
                SongTimer = 10.0,
                NoteData = new NoteData()
            },
            SongDetails = new SongDetails
            {
                SongName = "Test Song",
                ArtistName = "Test Artist",
                SongLength = 120.0,
                AlbumName = "Test Album",
                AlbumYear = 2020,
                Arrangements = new[]
                {
                    new Arrangement
                    {
                        Name = "Lead",
                        ArrangementID = "arr1",
                        type = "Lead",
                        Tuning = new Tuning { TuningName = "E Standard" },
                        Sections = new Section[0]
                    }
                }
            }
        };
        parser.SetResponse(response);

        var identified = parser.IdentifyArrangement();

        Assert.False(identified);
    }

    [Fact]
    public void PerformSceneSwitchIfNecessary_FromMenuToSong_SwitchesToSongScene()
    {
        var (parser, cph) = CreateParser(menuScene: "MenuScene", songScenes: "SongScene");
        cph.CurrentScene = "MenuScene";
        parser.SetCurrentScene("MenuScene");

        // First call: establish menu state
        var menuResponse = BuildResponse("MainMenu", 0.0);
        parser.SetResponse(menuResponse);
        parser.UpdateStageAndTimer();
        parser.PerformSceneSwitchIfNecessary();

        // Second call: transition to in-song
        var songResponse = BuildResponse("las_game", 1.0);
        parser.SetResponse(songResponse);
        parser.UpdateStageAndTimer();
        parser.PerformSceneSwitchIfNecessary();

        Assert.Equal("SongScene", cph.CurrentScene);
    }

    [Fact]
    public void PerformSceneSwitchIfNecessary_InSongToMenu_SwitchesToMenuScene()
    {
        var (parser, cph) = CreateParser(menuScene: "MenuScene", songScenes: "SongScene");
        cph.CurrentScene = "SongScene";
        parser.SetCurrentScene("SongScene");

        // Establish in-song state
        var songResponse = BuildResponse("las_game", 30.0);
        parser.SetResponse(songResponse);
        parser.UpdateStageAndTimer();
        parser.PerformSceneSwitchIfNecessary();

        // Transition to menu
        var menuResponse = BuildResponse("MainMenu", 0.0);
        parser.SetResponse(menuResponse);
        parser.UpdateStageAndTimer();
        parser.PerformSceneSwitchIfNecessary();

        Assert.Equal("MenuScene", cph.CurrentScene);
    }

    [Theory]
    [InlineData("solo_1", "Solo")]
    [InlineData("riff_A", "Riff")]
    [InlineData("noguitar", "NoGuitar")]
    [InlineData("bridge_2", "Bridge")]
    [InlineData("breakdown", "Breakdown")]
    [InlineData("chorus_A", "Chorus")]
    [InlineData("verse_1", "Verse")]
    public void CheckSectionActions_SectionNameMappedCorrectly_TriggersCorrectEnterEvent(
        string sectionName, string expectedSectionTypeName)
    {
        var args = new Dictionary<string, object>
        {
            [Constants.ArgumentNameMenuScene] = "MenuScene",
            [Constants.ArgumentNameSongScenes] = "SongScene",
            [Constants.ArgumentNamePauseScene] = "PauseScene",
            [Constants.ArgumentNameSwitchScenes] = "True",
            [Constants.ArgumentNameSongSceneAutoSwitchMode] = "Off",
            [Constants.ArgumentNameSectionActions] = "True",  // sectionActions enabled
            [Constants.ArgumentNameBehavior] = "AlwaysOn",
            [Constants.ArgumentNameSceneSwitchPeriod] = "5",
            [Constants.ArgumentNameSceneSwitchCooldownPeriod] = "0",
            [Constants.GlobalVarNameGuessingIsActive] = "False",
            [Constants.GlobalVarNameGuessingMinGuesser] = "2",
            [Constants.GlobalVarNameGuessingGuessTime] = "30",
            [Constants.ArgumentNameGuessingStartedText] = "Guessing started",
            [Constants.ArgumentNameGuessingTimeoutText] = "Guessing ended",
        };

        var cph = new TestCPH { CurrentScene = "SongScene" };
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesHitLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesMissedLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameAccuracyLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameGuessingWinnersCount] = "{}";
        // ReadArgumentAsStringArray uses CPH.TryGetArg, so the value must also be in GlobalVars
        cph.GlobalVars[Constants.ArgumentNameSongScenes] = "SongScene";

        var dataHandler = new CPHInline.DataHandler(cph, args);
        var sceneController = new CPHInline.SceneController(cph);
        sceneController.SetCooldownPeriod(0);
        // Initialize stream app by querying the current scene
        _ = sceneController.GetCurrentScene();
        var guessingGame = new CPHInline.GuessingGame(cph, dataHandler);
        var parser = new CPHInline.ResponseParser(cph, sceneController, guessingGame, dataHandler);
        parser.Init();
        parser.UpdateConfig();
        parser.SetCurrentScene("SongScene");

        var response = new Response
        {
            MemoryReadout = new MemoryReadout
            {
                SongId = "song1",
                ArrangementId = "arr1",
                GameStage = "las_game",
                SongTimer = 25.0,
                NoteData = new NoteData()
            },
            SongDetails = new SongDetails
            {
                SongName = "Test",
                ArtistName = "Artist",
                SongLength = 120.0,
                AlbumName = "Album",
                AlbumYear = 2020,
                Arrangements = new[]
                {
                    new Arrangement
                    {
                        Name = "Lead",
                        ArrangementID = "arr1",
                        type = "Lead",
                        Tuning = new Tuning { TuningName = "E Standard" },
                        Sections = new[]
                        {
                            new Section { Name = sectionName, StartTime = 0.0, EndTime = 60.0 }
                        }
                    }
                }
            }
        };

        parser.SetResponse(response);
        parser.UpdateStageAndTimer();
        parser.IdentifyArrangement();
        parser.CheckSectionActions();

        var enterEvent = $"enter{expectedSectionTypeName}";
        Assert.Contains(cph.TriggeredEvents, e => e.EventName == enterEvent);
    }

    [Fact]
    public void CheckSectionActions_IntroSection_DoesNotTriggerEnterDefaultBecauseDefaultIsInitialState()
    {
        // "intro" maps to SectionType.Default, which is also the initial lastSectionType value,
        // so no enter/leave events are triggered when the first section is Default.
        var args = new Dictionary<string, object>
        {
            [Constants.ArgumentNameMenuScene] = "MenuScene",
            [Constants.ArgumentNameSongScenes] = "SongScene",
            [Constants.ArgumentNamePauseScene] = "PauseScene",
            [Constants.ArgumentNameSwitchScenes] = "True",
            [Constants.ArgumentNameSongSceneAutoSwitchMode] = "Off",
            [Constants.ArgumentNameSectionActions] = "True",
            [Constants.ArgumentNameBehavior] = "AlwaysOn",
            [Constants.ArgumentNameSceneSwitchPeriod] = "5",
            [Constants.ArgumentNameSceneSwitchCooldownPeriod] = "0",
            [Constants.GlobalVarNameGuessingIsActive] = "False",
            [Constants.GlobalVarNameGuessingMinGuesser] = "2",
            [Constants.GlobalVarNameGuessingGuessTime] = "30",
            [Constants.ArgumentNameGuessingStartedText] = "Guessing started",
            [Constants.ArgumentNameGuessingTimeoutText] = "Guessing ended",
        };

        var cph = new TestCPH { CurrentScene = "SongScene" };
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesHitLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameTotalNotesMissedLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameAccuracyLifeTime] = "0";
        cph.GlobalVars[Constants.GlobalVarNameGuessingWinnersCount] = "{}";
        cph.GlobalVars[Constants.ArgumentNameSongScenes] = "SongScene";

        var dataHandler = new CPHInline.DataHandler(cph, args);
        var sceneController = new CPHInline.SceneController(cph);
        sceneController.SetCooldownPeriod(0);
        _ = sceneController.GetCurrentScene();
        var guessingGame = new CPHInline.GuessingGame(cph, dataHandler);
        var parser = new CPHInline.ResponseParser(cph, sceneController, guessingGame, dataHandler);
        parser.Init();
        parser.UpdateConfig();
        parser.SetCurrentScene("SongScene");

        var response = new Response
        {
            MemoryReadout = new MemoryReadout
            {
                SongId = "song1", ArrangementId = "arr1", GameStage = "las_game",
                SongTimer = 25.0, NoteData = new NoteData()
            },
            SongDetails = new SongDetails
            {
                SongName = "Test", ArtistName = "Artist", SongLength = 120.0,
                AlbumName = "Album", AlbumYear = 2020,
                Arrangements = new[]
                {
                    new Arrangement
                    {
                        Name = "Lead", ArrangementID = "arr1", type = "Lead",
                        Tuning = new Tuning { TuningName = "E Standard" },
                        Sections = new[] { new Section { Name = "intro", StartTime = 0.0, EndTime = 60.0 } }
                    }
                }
            }
        };

        parser.SetResponse(response);
        parser.UpdateStageAndTimer();
        parser.IdentifyArrangement();
        parser.CheckSectionActions();

        // "intro" maps to Default, which is the initial section type — no event should fire
        Assert.DoesNotContain(cph.TriggeredEvents, e => e.EventName == "enterDefault");
    }
}
