using Newtonsoft.Json;
using Xunit;

/// <summary>
/// Tests for the JSON deserialization of RockSniffer Response objects.
/// </summary>
public class ResponseDeserializationTests
{
    private const string SampleResponseJson = @"{
        ""MemoryReadout"": {
            ""SongId"": ""abc123"",
            ""ArrangementId"": ""arr456"",
            ""GameStage"": ""las_game"",
            ""SongTimer"": 45.5,
            ""NoteData"": {
                ""Accuracy"": 95.3,
                ""TotalNotes"": 200,
                ""TotalNotesHit"": 190,
                ""CurrentHitStreak"": 15,
                ""HighestHitStreak"": 30,
                ""TotalNotesMissed"": 10,
                ""CurrentMissStreak"": 0
            }
        },
        ""SongDetails"": {
            ""SongName"": ""Test Song"",
            ""ArtistName"": ""Test Artist"",
            ""SongLength"": 240.0,
            ""AlbumName"": ""Test Album"",
            ""AlbumYear"": 2020,
            ""Arrangements"": [
                {
                    ""Name"": ""Lead"",
                    ""ArrangementID"": ""arr456"",
                    ""type"": ""Lead"",
                    ""Tuning"": { ""TuningName"": ""E Standard"" },
                    ""Sections"": [
                        { ""Name"": ""intro"", ""StartTime"": 0.0, ""EndTime"": 20.0 },
                        { ""Name"": ""verse1"", ""StartTime"": 20.0, ""EndTime"": 60.0 },
                        { ""Name"": ""chorus1"", ""StartTime"": 60.0, ""EndTime"": 100.0 }
                    ]
                }
            ]
        }
    }";

    private const string MenuResponseJson = @"{
        ""MemoryReadout"": {
            ""SongId"": """",
            ""ArrangementId"": """",
            ""GameStage"": ""MainMenu"",
            ""SongTimer"": 0.0,
            ""NoteData"": {
                ""Accuracy"": 0.0,
                ""TotalNotes"": 0,
                ""TotalNotesHit"": 0,
                ""CurrentHitStreak"": 0,
                ""HighestHitStreak"": 0,
                ""TotalNotesMissed"": 0,
                ""CurrentMissStreak"": 0
            }
        },
        ""SongDetails"": {
            ""SongName"": """",
            ""ArtistName"": """",
            ""SongLength"": 0.0,
            ""AlbumName"": """",
            ""AlbumYear"": 0,
            ""Arrangements"": []
        }
    }";

    private const string TunerResponseJson = @"{
        ""MemoryReadout"": {
            ""SongId"": """",
            ""ArrangementId"": """",
            ""GameStage"": ""las_tuner"",
            ""SongTimer"": 0.0,
            ""NoteData"": {
                ""Accuracy"": 0.0,
                ""TotalNotes"": 0,
                ""TotalNotesHit"": 0,
                ""CurrentHitStreak"": 0,
                ""HighestHitStreak"": 0,
                ""TotalNotesMissed"": 0,
                ""CurrentMissStreak"": 0
            }
        },
        ""SongDetails"": {
            ""SongName"": """",
            ""ArtistName"": """",
            ""SongLength"": 0.0,
            ""AlbumName"": """",
            ""AlbumYear"": 0,
            ""Arrangements"": []
        }
    }";

    [Fact]
    public void DeserializeResponse_InSongJson_ParsesMemoryReadoutCorrectly()
    {
        var response = JsonConvert.DeserializeObject<Response>(SampleResponseJson);

        Assert.NotNull(response);
        Assert.NotNull(response!.MemoryReadout);
        Assert.Equal("abc123", response.MemoryReadout.SongId);
        Assert.Equal("arr456", response.MemoryReadout.ArrangementId);
        Assert.Equal("las_game", response.MemoryReadout.GameStage);
        Assert.Equal(45.5, response.MemoryReadout.SongTimer);
    }

    [Fact]
    public void DeserializeResponse_InSongJson_ParsesNoteDataCorrectly()
    {
        var response = JsonConvert.DeserializeObject<Response>(SampleResponseJson);

        Assert.NotNull(response!.MemoryReadout.NoteData);
        Assert.Equal(95.3, response.MemoryReadout.NoteData.Accuracy);
        Assert.Equal(200, response.MemoryReadout.NoteData.TotalNotes);
        Assert.Equal(190, response.MemoryReadout.NoteData.TotalNotesHit);
        Assert.Equal(10, response.MemoryReadout.NoteData.TotalNotesMissed);
        Assert.Equal(15, response.MemoryReadout.NoteData.CurrentHitStreak);
        Assert.Equal(30, response.MemoryReadout.NoteData.HighestHitStreak);
        Assert.Equal(0, response.MemoryReadout.NoteData.CurrentMissStreak);
    }

    [Fact]
    public void DeserializeResponse_InSongJson_ParsesSongDetailsCorrectly()
    {
        var response = JsonConvert.DeserializeObject<Response>(SampleResponseJson);

        Assert.NotNull(response!.SongDetails);
        Assert.Equal("Test Song", response.SongDetails.SongName);
        Assert.Equal("Test Artist", response.SongDetails.ArtistName);
        Assert.Equal(240.0, response.SongDetails.SongLength);
        Assert.Equal("Test Album", response.SongDetails.AlbumName);
        Assert.Equal(2020, response.SongDetails.AlbumYear);
    }

    [Fact]
    public void DeserializeResponse_InSongJson_ParsesArrangementCorrectly()
    {
        var response = JsonConvert.DeserializeObject<Response>(SampleResponseJson);

        Assert.NotNull(response!.SongDetails.Arrangements);
        Assert.Single(response.SongDetails.Arrangements);
        var arrangement = response.SongDetails.Arrangements[0];
        Assert.Equal("Lead", arrangement.Name);
        Assert.Equal("arr456", arrangement.ArrangementID);
        Assert.Equal("Lead", arrangement.type);
        Assert.Equal("E Standard", arrangement.Tuning.TuningName);
    }

    [Fact]
    public void DeserializeResponse_InSongJson_ParsesSectionsCorrectly()
    {
        var response = JsonConvert.DeserializeObject<Response>(SampleResponseJson);

        var sections = response!.SongDetails.Arrangements[0].Sections;
        Assert.Equal(3, sections.Length);
        Assert.Equal("intro", sections[0].Name);
        Assert.Equal(0.0, sections[0].StartTime);
        Assert.Equal(20.0, sections[0].EndTime);
        Assert.Equal("verse1", sections[1].Name);
        Assert.Equal("chorus1", sections[2].Name);
    }

    [Fact]
    public void DeserializeResponse_MenuJson_ParsesGameStageAsMainMenu()
    {
        var response = JsonConvert.DeserializeObject<Response>(MenuResponseJson);

        Assert.NotNull(response);
        Assert.Equal("MainMenu", response!.MemoryReadout.GameStage);
        Assert.Equal(0.0, response.MemoryReadout.SongTimer);
    }

    [Fact]
    public void DeserializeResponse_TunerJson_ParsesGameStageAsTuner()
    {
        var response = JsonConvert.DeserializeObject<Response>(TunerResponseJson);

        Assert.NotNull(response);
        Assert.Equal("las_tuner", response!.MemoryReadout.GameStage);
    }

    [Fact]
    public void DeserializeResponse_InvalidJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() =>
            JsonConvert.DeserializeObject<Response>("not valid json"));
    }
}
