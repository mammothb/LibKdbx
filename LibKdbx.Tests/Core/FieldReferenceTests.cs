namespace LibKdbx.Tests;

public class FieldReferenceTests
{
    [Theory]
    [InlineData(
        "{REF:P@I:ABCDEF1234567890ABCDEF1234567890}",
        'P',
        'I',
        "ABCDEF1234567890ABCDEF1234567890"
    )]
    [InlineData("{REF:U@T:sometitle}", 'U', 'T', "sometitle")]
    [InlineData("{REF:A@N:notes text}", 'A', 'N', "notes text")]
    [InlineData("{REF:T@U:alice}", 'T', 'U', "alice")]
    public void Parse_Valid(string value, char wantedField, char searchIn, string searchValue)
    {
        FieldReference.TryParse(value, out FieldReference result).ShouldBeTrue();
        result.WantedField.ShouldBe(wantedField);
        result.SearchIn.ShouldBe(searchIn);
        result.SearchValue.ShouldBe(searchValue);
    }

    [Theory]
    [InlineData("not a ref")]
    [InlineData("{NOT_A_REF}")]
    [InlineData("{REF:}")]
    [InlineData("")]
    public void Parse_Invalid(string value)
    {
        FieldReference.TryParse(value, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData('T', "Title")]
    [InlineData('U', "UserName")]
    [InlineData('P', "Password")]
    [InlineData('A', "URL")]
    [InlineData('N', "Notes")]
    [InlineData('t', "Title")]
    [InlineData('X', null)]
    public void FieldCodeToKey(char code, string? expected)
    {
        FieldReference.FieldCodeToKey(code).ShouldBe(expected);
    }
}
