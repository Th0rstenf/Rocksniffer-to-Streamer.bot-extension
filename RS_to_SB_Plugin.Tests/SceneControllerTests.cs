using System.Threading;
using Xunit;

/// <summary>
/// Tests for CPHInline.SceneController.
/// </summary>
public class SceneControllerTests
{
    [Fact]
    public void IsNotInCooldown_AfterCreation_ReturnsFalse()
    {
        // Right after creation the lastSceneChange is DateTime.Now, so we ARE in cooldown
        var cph = new TestCPH();
        var controller = new CPHInline.SceneController(cph);

        var result = controller.IsNotInCooldown();

        // Default cooldown is 3 seconds; no time has passed, so should be in cooldown
        Assert.False(result);
    }

    [Fact]
    public void IsNotInCooldown_AfterCooldownExpires_ReturnsTrue()
    {
        var cph = new TestCPH();
        var controller = new CPHInline.SceneController(cph);
        controller.SetCooldownPeriod(0); // zero-second cooldown expires immediately

        var result = controller.IsNotInCooldown();

        Assert.True(result);
    }

    [Fact]
    public void SetCooldownPeriod_ControlsCooldownBehavior()
    {
        var cph = new TestCPH();
        var controller = new CPHInline.SceneController(cph);
        controller.SetCooldownPeriod(0);

        Assert.True(controller.IsNotInCooldown());
    }

    [Fact]
    public void SwitchToScene_WhenNotInCooldownAndSwitchEnabled_ChangesCurrentScene()
    {
        var cph = new TestCPH { CurrentScene = "MenuScene" };
        var controller = new CPHInline.SceneController(cph);
        controller.SetCooldownPeriod(0);
        // GetCurrentScene initializes the stream app connection (OBS)
        _ = controller.GetCurrentScene();

        controller.SwitchToScene("SongScene", switchScenes: true);

        Assert.Equal("SongScene", cph.CurrentScene);
    }

    [Fact]
    public void SwitchToScene_WhenSwitchDisabled_DoesNotChangeScene()
    {
        var cph = new TestCPH { CurrentScene = "MenuScene" };
        var controller = new CPHInline.SceneController(cph);
        controller.SetCooldownPeriod(0);

        controller.SwitchToScene("SongScene", switchScenes: false);

        Assert.Equal("MenuScene", cph.CurrentScene);
    }

    [Fact]
    public void SwitchToScene_WhenInCooldown_DoesNotChangeScene()
    {
        var cph = new TestCPH { CurrentScene = "MenuScene" };
        var controller = new CPHInline.SceneController(cph);
        // Default cooldown is 3 seconds; no time has passed so we ARE in cooldown

        controller.SwitchToScene("SongScene", switchScenes: true);

        Assert.Equal("MenuScene", cph.CurrentScene);
    }

    [Fact]
    public void GetCurrentScene_WhenObsConnected_ReturnsObsScene()
    {
        var cph = new TestCPH { CurrentScene = "MyScene", ObsConnected = true };
        var controller = new CPHInline.SceneController(cph);

        var scene = controller.GetCurrentScene();

        Assert.Equal("MyScene", scene);
    }

    [Fact]
    public void GetCurrentScene_WhenObsNotConnected_ReturnsEmpty()
    {
        var cph = new TestCPH { ObsConnected = false };
        var controller = new CPHInline.SceneController(cph);

        var scene = controller.GetCurrentScene();

        Assert.Equal(string.Empty, scene);
    }
}
