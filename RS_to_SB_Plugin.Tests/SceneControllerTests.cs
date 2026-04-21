using Xunit;

/// <summary>
/// Tests for CPHInline.SceneController.
/// </summary>
public class SceneControllerTests
{
    [Fact]
    public void IsNotInCooldown_AfterCreation_ReturnsFalse()
    {
        // Given a freshly created SceneController with the default 3-second cooldown
        var cph = new TestCPH();
        var controller = new CPHInline.SceneController(cph);

        // When checking whether the cooldown has expired immediately after creation
        var result = controller.IsNotInCooldown();

        // Then the controller reports it IS in cooldown (no time has passed)
        Assert.False(result);
    }

    [Fact]
    public void IsNotInCooldown_AfterCooldownExpires_ReturnsTrue()
    {
        // Given a SceneController whose cooldown period is set to zero seconds
        var cph = new TestCPH();
        var controller = new CPHInline.SceneController(cph);
        controller.SetCooldownPeriod(0);

        // When checking whether the cooldown has expired
        var result = controller.IsNotInCooldown();

        // Then the controller reports it is NOT in cooldown
        Assert.True(result);
    }

    [Fact]
    public void SwitchToScene_WhenNotInCooldownAndSwitchEnabled_ChangesCurrentScene()
    {
        // Given a SceneController with zero cooldown, OBS connected, and currently on "MenuScene"
        var cph = new TestCPH { CurrentScene = "MenuScene" };
        var controller = new CPHInline.SceneController(cph);
        controller.SetCooldownPeriod(0);
        // Querying the current scene initialises the internal OBS stream-app reference
        _ = controller.GetCurrentScene();

        // When switching to "SongScene" with scene-switching enabled
        controller.SwitchToScene("SongScene", switchScenes: true);

        // Then OBS is instructed to change to "SongScene"
        Assert.Equal("SongScene", cph.CurrentScene);
    }

    [Fact]
    public void SwitchToScene_WhenSwitchDisabled_DoesNotChangeScene()
    {
        // Given a SceneController with zero cooldown and scene-switching globally disabled
        var cph = new TestCPH { CurrentScene = "MenuScene" };
        var controller = new CPHInline.SceneController(cph);
        controller.SetCooldownPeriod(0);

        // When requesting a scene switch with switchScenes = false
        controller.SwitchToScene("SongScene", switchScenes: false);

        // Then the current scene is left unchanged
        Assert.Equal("MenuScene", cph.CurrentScene);
    }

    [Fact]
    public void SwitchToScene_WhenInCooldown_DoesNotChangeScene()
    {
        // Given a SceneController that was just created (default 3-second cooldown active)
        var cph = new TestCPH { CurrentScene = "MenuScene" };
        var controller = new CPHInline.SceneController(cph);

        // When requesting a scene switch before the cooldown has elapsed
        controller.SwitchToScene("SongScene", switchScenes: true);

        // Then the current scene is left unchanged to respect the cooldown
        Assert.Equal("MenuScene", cph.CurrentScene);
    }

    [Fact]
    public void GetCurrentScene_WhenObsConnected_ReturnsObsScene()
    {
        // Given OBS is connected and its active scene is "MyScene"
        var cph = new TestCPH { CurrentScene = "MyScene", ObsConnected = true };
        var controller = new CPHInline.SceneController(cph);

        // When retrieving the current scene
        var scene = controller.GetCurrentScene();

        // Then the OBS scene name is returned
        Assert.Equal("MyScene", scene);
    }

    [Fact]
    public void GetCurrentScene_WhenObsNotConnected_ReturnsEmpty()
    {
        // Given OBS is not connected (and neither is SLOBS)
        var cph = new TestCPH { ObsConnected = false };
        var controller = new CPHInline.SceneController(cph);

        // When retrieving the current scene
        var scene = controller.GetCurrentScene();

        // Then an empty string is returned because no stream app is available
        Assert.Equal(string.Empty, scene);
    }
}
