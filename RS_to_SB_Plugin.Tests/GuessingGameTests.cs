using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Xunit;

/// <summary>
/// Tests for CPHInline.GuessingGame winner determination and guessing lifecycle.
/// </summary>
public class GuessingGameTests
{
    private static (CPHInline.GuessingGame game, CPHInline.DataHandler dataHandler, TestCPH cph)
        CreateGuessingGame(bool isActive = true, int minimumGuesses = 1, int guessTimeoutSeconds = 30)
    {
        var cph = new TestCPH();
        cph.GlobalVars[Constants.GlobalVarNameGuessingWinnersCount] = "{}";

        var args = new Dictionary<string, object>
        {
            [Constants.GlobalVarNameGuessingIsActive] = isActive.ToString(),
            [Constants.GlobalVarNameGuessingMinGuesser] = minimumGuesses.ToString(),
            [Constants.GlobalVarNameGuessingGuessTime] = guessTimeoutSeconds.ToString(),
            [Constants.ArgumentNameGuessingStartedText] = "Guessing started",
            [Constants.ArgumentNameGuessingTimeoutText] = "No more guesses",
        };

        var dataHandler = new CPHInline.DataHandler(cph, args);
        var game = new CPHInline.GuessingGame(cph, dataHandler);
        game.UpdateConfig();
        return (game, dataHandler, cph);
    }

    private static NoteData BuildNoteData(double accuracy, int totalNotes = 100, int totalNotesHit = 90, int totalNotesMissed = 10)
    {
        return new NoteData
        {
            Accuracy = accuracy,
            TotalNotes = totalNotes,
            TotalNotesHit = totalNotesHit,
            TotalNotesMissed = totalNotesMissed,
            CurrentHitStreak = 0,
            HighestHitStreak = 0,
            CurrentMissStreak = 0
        };
    }

    [Fact]
    public void FinishAndEvaluate_WhenNotActive_DoesNotTriggerWinnerEvent()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: false);

        game.FinishAndEvaluate(120.0, BuildNoteData(90.0));

        Assert.Empty(cph.TriggeredEvents);
    }

    [Fact]
    public void FinishAndEvaluate_WhenSongTooShort_SendsMessageAboutTooShortSong()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: true, guessTimeoutSeconds: 30);

        // Song length (10s) is less than timeout (30s)
        game.FinishAndEvaluate(totalLength: 10.0, BuildNoteData(90.0));

        Assert.Contains(cph.SentMessages, m => m.Contains("too short"));
    }

    [Fact]
    public void FinishAndEvaluate_WhenNotEnoughGuesses_SendsNotEnoughGuessesMessage()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: true, minimumGuesses: 2, guessTimeoutSeconds: 30);

        game.StartAcceptingGuesses();
        // Set up one guess (not enough, need 2)
        var guessDict = new Dictionary<string, float> { ["Alice"] = 90.0f };
        cph.GlobalVars[Constants.GlobalVarNameGuessingDictionary] = JsonConvert.SerializeObject(guessDict);
        game.CheckTimeout(31); // Stops accepting guesses and loads them

        game.FinishAndEvaluate(totalLength: 120.0, BuildNoteData(90.0));

        Assert.Contains(cph.SentMessages, m => m.Contains("only") || m.Contains("out of"));
    }

    [Fact]
    public void FinishAndEvaluate_WhenOneGuesserWithEnoughGuessCount_TriggersWinnerEvent()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: true, minimumGuesses: 1, guessTimeoutSeconds: 30);

        game.StartAcceptingGuesses();
        var guessDict = new Dictionary<string, float> { ["Alice"] = 90.0f };
        cph.GlobalVars[Constants.GlobalVarNameGuessingDictionary] = JsonConvert.SerializeObject(guessDict);
        game.CheckTimeout(31);

        game.FinishAndEvaluate(totalLength: 120.0, BuildNoteData(accuracy: 92.0));

        Assert.Contains(cph.TriggeredEvents, e => e.EventName == Constants.TriggerNameGuessWinnerDetermined);
    }

    [Fact]
    public void FinishAndEvaluate_ClosestGuesserWins()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: true, minimumGuesses: 1, guessTimeoutSeconds: 30);

        game.StartAcceptingGuesses();
        var guessDict = new Dictionary<string, float>
        {
            ["Alice"] = 90.0f,  // deviation: |92 - 90| = 2
            ["Bob"] = 80.0f,    // deviation: |92 - 80| = 12
            ["Charlie"] = 91.5f // deviation: |92 - 91.5| = 0.5 (closest)
        };
        cph.GlobalVars[Constants.GlobalVarNameGuessingDictionary] = JsonConvert.SerializeObject(guessDict);
        game.CheckTimeout(31);

        game.FinishAndEvaluate(totalLength: 120.0, BuildNoteData(accuracy: 92.0));

        var winnerEvent = cph.TriggeredEvents
            .FirstOrDefault(e => e.EventName == Constants.TriggerNameGuessWinnerDetermined);
        Assert.NotNull(winnerEvent.EventName);

        var eventArgs = (Dictionary<string, object>)winnerEvent.Args;
        Assert.Equal("Charlie", eventArgs[Constants.ArgumentNameGuessingWinner]);
    }

    [Fact]
    public void StartAcceptingGuesses_WhenActive_StartsGuessingSession()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: true);

        game.StartAcceptingGuesses();

        Assert.Contains(cph.SentMessages, m => m == "Guessing started");
    }

    [Fact]
    public void StartAcceptingGuesses_WhenNotActive_DoesNotStartSession()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: false);

        game.StartAcceptingGuesses();

        Assert.DoesNotContain(cph.SentMessages, m => m == "Guessing started");
    }

    [Fact]
    public void CheckTimeout_WhenActiveAndTimerExceedsTimeout_StopsAcceptingGuesses()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: true, guessTimeoutSeconds: 30);
        game.StartAcceptingGuesses();

        game.CheckTimeout(31);

        Assert.Contains(cph.SentMessages, m => m == "No more guesses");
    }

    [Fact]
    public void CheckTimeout_WhenTimerBelowTimeout_DoesNotStopAcceptingGuesses()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: true, guessTimeoutSeconds: 30);
        game.StartAcceptingGuesses();

        game.CheckTimeout(15);

        Assert.DoesNotContain(cph.SentMessages, m => m == "No more guesses");
    }

    [Fact]
    public void FinishAndEvaluate_WhenSongNotPlayedToEnd_SendsAppropriateMessage()
    {
        var (game, _, cph) = CreateGuessingGame(isActive: true, minimumGuesses: 1, guessTimeoutSeconds: 30);
        game.StartAcceptingGuesses();
        var guessDict = new Dictionary<string, float> { ["Alice"] = 90.0f };
        cph.GlobalVars[Constants.GlobalVarNameGuessingDictionary] = JsonConvert.SerializeObject(guessDict);
        game.CheckTimeout(31);

        // TotalNotes (100) > TotalNotesHit (50) + TotalNotesMissed (30) means song was incomplete
        var noteData = new NoteData
        {
            Accuracy = 85.0,
            TotalNotes = 100,
            TotalNotesHit = 50,
            TotalNotesMissed = 30,
        };
        game.FinishAndEvaluate(totalLength: 120.0, noteData);

        Assert.Contains(cph.SentMessages, m => m.Contains("not played to the end"));
    }
}
