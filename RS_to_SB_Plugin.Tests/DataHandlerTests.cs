using System.Collections.Generic;
using Xunit;

/// <summary>
/// Tests for CPHInline.DataHandler argument reading methods.
/// </summary>
public class DataHandlerTests
{
    private static CPHInline.DataHandler CreateDataHandler(Dictionary<string, object> args)
    {
        var cph = new TestCPH();
        return new CPHInline.DataHandler(cph, args);
    }

    [Fact]
    public void ReadArgumentAsString_ExistingKey_ReturnsValue()
    {
        var args = new Dictionary<string, object> { ["key"] = "hello" };
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsString("key");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void ReadArgumentAsString_MissingKey_ReturnsEmptyString()
    {
        var args = new Dictionary<string, object>();
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsString("missing");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ReadArgumentAsInt_ValidIntegerString_ReturnsInteger()
    {
        var args = new Dictionary<string, object> { ["count"] = "42" };
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsInt("count");

        Assert.Equal(42, result);
    }

    [Fact]
    public void ReadArgumentAsInt_InvalidString_ReturnsZero()
    {
        var args = new Dictionary<string, object> { ["count"] = "notanumber" };
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsInt("count");

        Assert.Equal(0, result);
    }

    [Fact]
    public void ReadArgumentAsInt_MissingKey_ReturnsZero()
    {
        var args = new Dictionary<string, object>();
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsInt("missing");

        Assert.Equal(0, result);
    }

    [Fact]
    public void ReadArgumentAsBool_TrueString_ReturnsTrue()
    {
        var args = new Dictionary<string, object> { ["flag"] = "True" };
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsBool("flag");

        Assert.True(result);
    }

    [Fact]
    public void ReadArgumentAsBool_FalseString_ReturnsFalse()
    {
        var args = new Dictionary<string, object> { ["flag"] = "False" };
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsBool("flag");

        Assert.False(result);
    }

    [Fact]
    public void ReadArgumentAsBool_MissingKey_ReturnsFalse()
    {
        var args = new Dictionary<string, object>();
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsBool("missing");

        Assert.False(result);
    }

    [Fact]
    public void ReadArgumentAsFloat_ValidFloatString_ReturnsFloat()
    {
        var args = new Dictionary<string, object> { ["ratio"] = "3.14" };
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsFloat("ratio");

        Assert.InRange(result, 3.13f, 3.15f);
    }

    [Fact]
    public void ReadArgumentAsFloat_InvalidString_ReturnsZero()
    {
        var args = new Dictionary<string, object> { ["ratio"] = "invalid" };
        var handler = CreateDataHandler(args);

        var result = handler.ReadArgumentAsFloat("ratio");

        Assert.Equal(0f, result);
    }

    [Fact]
    public void ReadArgumentAsStringArray_CommaSeparated_ReturnsSplitValues()
    {
        var cph = new TestCPH();
        cph.GlobalVars["scenes"] = "Scene1, Scene2, Scene3";
        var args = new Dictionary<string, object> { ["scenes"] = "Scene1, Scene2, Scene3" };
        var handler = new CPHInline.DataHandler(cph, args);

        var result = handler.ReadArgumentAsStringArray("scenes");

        Assert.NotNull(result);
        Assert.Equal(3, result!.Length);
        Assert.Equal("Scene1", result[0]);
        Assert.Equal("Scene2", result[1]);
        Assert.Equal("Scene3", result[2]);
    }

    [Fact]
    public void ReadArgumentAsStringArray_SemicolonSeparated_ReturnsSplitValues()
    {
        var cph = new TestCPH();
        cph.GlobalVars["scenes"] = "Scene1;Scene2;Scene3";
        var args = new Dictionary<string, object> { ["scenes"] = "Scene1;Scene2;Scene3" };
        var handler = new CPHInline.DataHandler(cph, args);

        var result = handler.ReadArgumentAsStringArray("scenes");

        Assert.NotNull(result);
        Assert.Equal(3, result!.Length);
        Assert.Equal("Scene1", result[0]);
        Assert.Equal("Scene2", result[1]);
        Assert.Equal("Scene3", result[2]);
    }

    [Fact]
    public void ReadArgumentAsStringArray_EmptyString_ReturnsNull()
    {
        var cph = new TestCPH();
        cph.GlobalVars["scenes"] = "";
        var args = new Dictionary<string, object> { ["scenes"] = "" };
        var handler = new CPHInline.DataHandler(cph, args);

        var result = handler.ReadArgumentAsStringArray("scenes");

        Assert.Null(result);
    }

    [Fact]
    public void SetArguments_UpdatesArgumentsForSubsequentReads()
    {
        var args = new Dictionary<string, object> { ["key"] = "initial" };
        var handler = CreateDataHandler(args);

        var newArgs = new Dictionary<string, object> { ["key"] = "updated" };
        handler.setArguments(newArgs);
        var result = handler.ReadArgumentAsString("key");

        Assert.Equal("updated", result);
    }
}
