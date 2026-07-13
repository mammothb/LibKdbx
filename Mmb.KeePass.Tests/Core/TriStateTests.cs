namespace Mmb.KeePass.Tests;

public class TriStateTests
{
    [Fact]
    public void TriState_Default_Is_Inherit()
    {
        default(TriState).ShouldBe(TriState.Inherit);
    }

    [Fact]
    public void TriState_All_Values_Are_Distinct()
    {
        TriState[] values = Enum.GetValues<TriState>();
        values.ShouldBeUnique();
        values.Length.ShouldBe(3);
    }

    [Theory]
    [InlineData("null", TriState.Inherit)]
    [InlineData("True", TriState.Enable)]
    [InlineData("False", TriState.Disable)]
    [InlineData("true", TriState.Enable)]
    [InlineData("false", TriState.Disable)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("NULl", TriState.Inherit)]
    [InlineData("garbage", null)]
    public void TriState_Parse_From_Xml(string xml, TriState? expected)
    {
        ParseTriState(xml).ShouldBe(expected);
    }

    [Theory]
    [InlineData(TriState.Inherit, "null")]
    [InlineData(TriState.Enable, "True")]
    [InlineData(TriState.Disable, "False")]
    public void TriState_ToXml(TriState value, string expected)
    {
        FormatTriState(value).ShouldBe(expected);
    }

    // ── Helpers (mirrors logic that will live in KdbxXmlReader/Writer) ──────

    internal static TriState? ParseTriState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "NULL" => TriState.Inherit,
            "TRUE" => TriState.Enable,
            "FALSE" => TriState.Disable,
            _ => null
        };
    }

    internal static string FormatTriState(TriState value) => value switch
    {
        TriState.Inherit => "null",
        TriState.Enable => "True",
        TriState.Disable => "False",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
