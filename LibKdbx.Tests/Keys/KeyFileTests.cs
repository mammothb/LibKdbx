using System.Text;

namespace LibKdbx.Tests;

public class KeyFileTests
{
    [Fact]
    public void Generate_XmlV1_Yields_Valid_KeyFile()
    {
        string path = Path.GetTempFileName();
        try
        {
            KeyFile.Generate(path, KeyFileFormat.Xml);

            string xml = File.ReadAllText(path, Encoding.UTF8);
            // Should contain KeyFile > Key > Data with base64
            xml.ShouldContain("<KeyFile>");
            xml.ShouldContain("<Key>");
            xml.ShouldContain("<Data>");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Generate_Raw_Yields_32_Bytes()
    {
        string path = Path.GetTempFileName();
        try
        {
            KeyFile.Generate(path, KeyFileFormat.Raw);
            byte[] data = File.ReadAllBytes(path);
            data.Length.ShouldBe(32);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
