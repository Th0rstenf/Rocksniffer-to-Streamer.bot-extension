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
        // Given an argument dictionary containing key "key" with value "hello"
        var args = new Dictionary<string, object> { ["key"] = "hello" };
        var handler = CreateDataHandler(args);

        // When reading the argument as a string
        var result = handler.ReadArgumentAsString("key");

        // Then the stored value is returned
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ReadArgumentAsString_MissingKey_ReturnsEmptyString()
    {
        // Given an empty argument dictionary
        var args = new Dictionary<string, object>();
        var handler = CreateDataHandler(args);

        // When reading a key that does not exist
        var result = handler.ReadArgumentAsString("missing");

        // Then an empty string is returned instead of throwing
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ReadArgumentAsInt_ValidIntegerString_ReturnsInteger()
    {
        // Given an argument whose value is the string "42"
        var args = new Dictionary<string, object> { ["count"] = "42" };
        var handler = CreateDataHandler(args);

        // When reading the argument as an integer
        var result = handler.ReadArgumentAsInt("count");

        // Then the numeric value 42 is returned
        Assert.Equal(42, result);
    }

    [Fact]
    public void ReadArgumentAsInt_InvalidString_ReturnsZero()
    {
        // Given an argument whose value cannot be parsed as an integer
        var args = new Dictionary<string, object> { ["count"] = "notanumber" };
        var handler = CreateDataHandler(args);

        // When reading the argument as an integer
        var result = handler.ReadArgumentAsInt("count");

        // Then zero is returned as the safe default
        Assert.Equal(0, result);
    }

    [Fact]
    public void ReadArgumentAsInt_MissingKey_ReturnsZero()
    {
        // Given an empty argument dictionary
        var args = new Dictionary<string, object>();
        var handler = CreateDataHandler(args);

        // When reading a missing key as an integer
        var result = handler.ReadArgumentAsInt("missing");

        // Then zero is returned as the safe default
        Assert.Equal(0, result);
    }

    [Fact]
    public void ReadArgumentAsBool_TrueString_ReturnsTrue()
    {
        // Given an argument whose value is the string "True"
        var args = new Dictionary<string, object> { ["flag"] = "True" };
        var handler = CreateDataHandler(args);

        // When reading the argument as a boolean
        var result = handler.ReadArgumentAsBool("flag");

        // Then true is returned
        Assert.True(result);
    }

    [Fact]
    public void ReadArgumentAsBool_FalseString_ReturnsFalse()
    {
        // Given an argument whose value is the string "False"
        var args = new Dictionary<string, object> { ["flag"] = "False" };
        var handler = CreateDataHandler(args);

        // When reading the argument as a boolean
        var result = handler.ReadArgumentAsBool("flag");

        // Then false is returned
        Assert.False(result);
    }

    [Fact]
    public void ReadArgumentAsBool_MissingKey_ReturnsFalse()
    {
        // Given an empty argument dictionary
        var args = new Dictionary<string, object>();
        var handler = CreateDataHandler(args);

        // When reading a missing key as a boolean
        var result = handler.ReadArgumentAsBool("missing");

        // Then false is returned as the safe default
        Assert.False(result);
    }

    [Fact]
    public void ReadArgumentAsFloat_ValidFloatString_ReturnsFloat()
    {
        // Given an argument whose value is the string "3.14"
        var args = new Dictionary<string, object> { ["ratio"] = "3.14" };
        var handler = CreateDataHandler(args);

        // When reading the argument as a float
        var result = handler.ReadArgumentAsFloat("ratio");

        // Then the numeric value ~3.14 is returned
        Assert.InRange(result, 3.13f, 3.15f);
    }

    [Fact]
    public void ReadArgumentAsFloat_InvalidString_ReturnsZero()
    {
        // Given an argument whose value cannot be parsed as a float
        var args = new Dictionary<string, object> { ["ratio"] = "invalid" };
        var handler = CreateDataHandler(args);

        // When reading the argument as a float
        var result = handler.ReadArgumentAsFloat("ratio");

        // Then zero is returned as the safe default
        Assert.Equal(0f, result);
    }

    [Fact]
    public void ReadArgumentAsStringArray_CommaSeparated_ReturnsSplitValues()
    {
        // Given an argument containing a comma-separated list of scene names
        var cph = new TestCPH();
        cph.GlobalVars["scenes"] = "Scene1, Scene2, Scene3";
        var args = new Dictionary<string, object> { ["scenes"] = "Scene1, Scene2, Scene3" };
        var handler = new CPHInline.DataHandler(cph, args);

        // When reading the argument as a string array
        var result = handler.ReadArgumentAsStringArray("scenes");

        // Then three individual scene names are returned, with whitespace trimmed
        Assert.NotNull(result);
        Assert.Equal(3, result!.Length);
        Assert.Equal("Scene1", result[0]);
        Assert.Equal("Scene2", result[1]);
        Assert.Equal("Scene3", result[2]);
    }

    [Fact]
    public void ReadArgumentAsStringArray_SemicolonSeparated_ReturnsSplitValues()
    {
        // Given an argument containing a semicolon-separated list of scene names
        var cph = new TestCPH();
        cph.GlobalVars["scenes"] = "Scene1;Scene2;Scene3";
        var args = new Dictionary<string, object> { ["scenes"] = "Scene1;Scene2;Scene3" };
        var handler = new CPHInline.DataHandler(cph, args);

        // When reading the argument as a string array
        var result = handler.ReadArgumentAsStringArray("scenes");

        // Then three individual scene names are returned
        Assert.NotNull(result);
        Assert.Equal(3, result!.Length);
        Assert.Equal("Scene1", result[0]);
        Assert.Equal("Scene2", result[1]);
        Assert.Equal("Scene3", result[2]);
    }

    [Fact]
    public void ReadArgumentAsStringArray_EmptyString_ReturnsNull()
    {
        // Given an argument whose value is an empty string
        var cph = new TestCPH();
        cph.GlobalVars["scenes"] = "";
        var args = new Dictionary<string, object> { ["scenes"] = "" };
        var handler = new CPHInline.DataHandler(cph, args);

        // When reading the argument as a string array
        var result = handler.ReadArgumentAsStringArray("scenes");

        // Then null is returned, signalling no values are present
        Assert.Null(result);
    }

    [Fact]
    public void SetArguments_UpdatesArgumentsForSubsequentReads()
    {
        // Given a handler that was created with an initial argument value
        var args = new Dictionary<string, object> { ["key"] = "initial" };
        var handler = CreateDataHandler(args);

        // When the argument dictionary is replaced with a new one containing an updated value
        var newArgs = new Dictionary<string, object> { ["key"] = "updated" };
        handler.setArguments(newArgs);
        var result = handler.ReadArgumentAsString("key");

        // Then subsequent reads return the value from the new dictionary
        Assert.Equal("updated", result);
    }
}
