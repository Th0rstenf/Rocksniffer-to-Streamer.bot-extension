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
        // Given a guessing game that is disabled
        var (game, _, cph) = CreateGuessingGame(isActive: false);

        // When the song ends and the game is asked to evaluate
        game.FinishAndEvaluate(120.0, BuildNoteData(90.0));

        // Then no winner event is triggered because the feature is off
        Assert.Empty(cph.TriggeredEvents);
    }

    [Fact]
    public void FinishAndEvaluate_WhenSongTooShort_SendsMessageAboutTooShortSong()
    {
        // Given a guessing game with a 30-second guess timeout
        var (game, _, cph) = CreateGuessingGame(isActive: true, guessTimeoutSeconds: 30);

        // When the song ends but its total length (10 s) is shorter than the guess window (30 s)
        game.FinishAndEvaluate(totalLength: 10.0, BuildNoteData(90.0));

        // Then a "too short" notice is broadcast to chat
        Assert.Contains(cph.SentMessages, m => m.Contains("too short"));
    }

    [Fact]
    public void FinishAndEvaluate_WhenNotEnoughGuesses_SendsNotEnoughGuessesMessage()
    {
        // Given a guessing game that requires at least 2 guesses
        var (game, _, cph) = CreateGuessingGame(isActive: true, minimumGuesses: 2, guessTimeoutSeconds: 30);
        game.StartAcceptingGuesses();
        // Only one viewer submitted a guess
        var guessDict = new Dictionary<string, float> { ["Alice"] = 90.0f };
        cph.GlobalVars[Constants.GlobalVarNameGuessingDictionary] = JsonConvert.SerializeObject(guessDict);
        game.CheckTimeout(31);

        // When the song ends with only one guess recorded
        game.FinishAndEvaluate(totalLength: 120.0, BuildNoteData(90.0));

        // Then a message is sent explaining not enough viewers participated
        Assert.Contains(cph.SentMessages, m => m.Contains("only") || m.Contains("out of"));
    }

    [Fact]
    public void FinishAndEvaluate_WhenOneGuesserWithEnoughGuessCount_TriggersWinnerEvent()
    {
        // Given a guessing game with a minimum of 1 guess and one guess submitted
        var (game, _, cph) = CreateGuessingGame(isActive: true, minimumGuesses: 1, guessTimeoutSeconds: 30);
        game.StartAcceptingGuesses();
        var guessDict = new Dictionary<string, float> { ["Alice"] = 90.0f };
        cph.GlobalVars[Constants.GlobalVarNameGuessingDictionary] = JsonConvert.SerializeObject(guessDict);
        game.CheckTimeout(31);

        // When the song ends with a final accuracy of 92 %
        game.FinishAndEvaluate(totalLength: 120.0, BuildNoteData(accuracy: 92.0));

        // Then the winner-determined trigger is fired
        Assert.Contains(cph.TriggeredEvents, e => e.EventName == Constants.TriggerNameGuessWinnerDetermined);
    }

    [Fact]
    public void FinishAndEvaluate_ClosestGuesserWins()
    {
        // Given three viewers with different accuracy guesses
        var (game, _, cph) = CreateGuessingGame(isActive: true, minimumGuesses: 1, guessTimeoutSeconds: 30);
        game.StartAcceptingGuesses();
        var guessDict = new Dictionary<string, float>
        {
            ["Alice"]   = 90.0f,  // deviation: |92 - 90|   = 2.0
            ["Bob"]     = 80.0f,  // deviation: |92 - 80|   = 12.0
            ["Charlie"] = 91.5f   // deviation: |92 - 91.5| = 0.5  ← closest
        };
        cph.GlobalVars[Constants.GlobalVarNameGuessingDictionary] = JsonConvert.SerializeObject(guessDict);
        game.CheckTimeout(31);

        // When the song ends with a final accuracy of 92 %
        game.FinishAndEvaluate(totalLength: 120.0, BuildNoteData(accuracy: 92.0));

        // Then the event args identify Charlie as the winner
        var winnerEvent = cph.TriggeredEvents
            .FirstOrDefault(e => e.EventName == Constants.TriggerNameGuessWinnerDetermined);
        Assert.NotNull(winnerEvent.EventName);
        var eventArgs = (Dictionary<string, object>)winnerEvent.Args;
        Assert.Equal("Charlie", eventArgs[Constants.ArgumentNameGuessingWinner]);
    }

    [Fact]
    public void StartAcceptingGuesses_WhenActive_StartsGuessingSession()
    {
        // Given an active guessing game
        var (game, _, cph) = CreateGuessingGame(isActive: true);

        // When the song starts and guesses begin to be accepted
        game.StartAcceptingGuesses();

        // Then the "Guessing started" announcement is broadcast to chat
        Assert.Contains(cph.SentMessages, m => m == "Guessing started");
    }

    [Fact]
    public void StartAcceptingGuesses_WhenNotActive_DoesNotStartSession()
    {
        // Given a guessing game that is disabled
        var (game, _, cph) = CreateGuessingGame(isActive: false);

        // When StartAcceptingGuesses is called anyway
        game.StartAcceptingGuesses();

        // Then no announcement is sent because the feature is off
        Assert.DoesNotContain(cph.SentMessages, m => m == "Guessing started");
    }

    [Fact]
    public void CheckTimeout_WhenActiveAndTimerExceedsTimeout_StopsAcceptingGuesses()
    {
        // Given an active guessing session with a 30-second window
        var (game, _, cph) = CreateGuessingGame(isActive: true, guessTimeoutSeconds: 30);
        game.StartAcceptingGuesses();

        // When the song timer advances past the timeout (31 s > 30 s)
        game.CheckTimeout(31);

        // Then the timeout message is broadcast and guessing is closed
        Assert.Contains(cph.SentMessages, m => m == "No more guesses");
    }

    [Fact]
    public void CheckTimeout_WhenTimerBelowTimeout_DoesNotStopAcceptingGuesses()
    {
        // Given an active guessing session with a 30-second window
        var (game, _, cph) = CreateGuessingGame(isActive: true, guessTimeoutSeconds: 30);
        game.StartAcceptingGuesses();

        // When the song timer is still within the guess window (15 s < 30 s)
        game.CheckTimeout(15);

        // Then guessing remains open and no timeout message is sent
        Assert.DoesNotContain(cph.SentMessages, m => m == "No more guesses");
    }

    [Fact]
    public void FinishAndEvaluate_WhenSongNotPlayedToEnd_SendsAppropriateMessage()
    {
        // Given a guessing session that has locked in one guess
        var (game, _, cph) = CreateGuessingGame(isActive: true, minimumGuesses: 1, guessTimeoutSeconds: 30);
        game.StartAcceptingGuesses();
        var guessDict = new Dictionary<string, float> { ["Alice"] = 90.0f };
        cph.GlobalVars[Constants.GlobalVarNameGuessingDictionary] = JsonConvert.SerializeObject(guessDict);
        game.CheckTimeout(31);

        // When the song ends but note totals show it was not played to completion
        // (TotalNotes 100 > TotalNotesHit 50 + TotalNotesMissed 30 means unplayed notes remain)
        var noteData = new NoteData
        {
            Accuracy = 85.0,
            TotalNotes = 100,
            TotalNotesHit = 50,
            TotalNotesMissed = 30,
        };
        game.FinishAndEvaluate(totalLength: 120.0, noteData);

        // Then a message is sent explaining the song was not fully played
        Assert.Contains(cph.SentMessages, m => m.Contains("not played to the end"));
    }
}
