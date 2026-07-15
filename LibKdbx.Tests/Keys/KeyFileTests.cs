namespace LibKdbx.Tests;

public class KeyFileTests
{
    [Fact]
    public void Generate_XmlV1_Yields_Valid_KeyFile()
    {
        using var tf = new TempFile();
        KeyFile.Generate(tf.Path, KeyFileFormat.Xml);

        string xml = tf.ReadAllText();
        // Should contain KeyFile > Key > Data with base64
        xml.ShouldContain("<KeyFile>");
        xml.ShouldContain("<Key>");
        xml.ShouldContain("<Data>");
    }

    [Fact]
    public void Generate_Raw_Yields_32_Bytes()
    {
        using var tf = new TempFile();
        KeyFile.Generate(tf.Path, KeyFileFormat.Raw);
        byte[] data = tf.ReadAllBytes();
        data.Length.ShouldBe(32);
    }
}
