namespace LibKdbx.Tests;

public class VariantMapTests
{
    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var original = new VariantMap(
            new Dictionary<string, object>
            {
                ["$UUID"] = GuidRfc4122.ToBytes(Guid.NewGuid()),
                ["S"] = new byte[] { 1, 2, 3, 4, 5 },
                ["P"] = (uint)4,
                ["M"] = (ulong)1024 * 1024 * 64,
                ["I"] = (ulong)3,
                ["V"] = (uint)0x13,
                ["Name"] = "TestDB",
                ["Flag"] = true,
            }
        );

        byte[] serialized = original.Serialize();
        var deserialized = VariantMap.Read(serialized);

        ((uint)deserialized["P"]).ShouldBe((uint)4);
        ((ulong)deserialized["M"]).ShouldBe((ulong)1024 * 1024 * 64);
        ((ulong)deserialized["I"]).ShouldBe((ulong)3);
        ((string)deserialized["Name"]).ShouldBe("TestDB");
        ((bool)deserialized["Flag"]).ShouldBe(true);
    }

    // ── Additional types ───────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Int32()
    {
        var original = new VariantMap(
            new Dictionary<string, object> { ["Count"] = -42, ["Offset"] = 0 }
        );
        byte[] serialized = original.Serialize();
        var deserialized = VariantMap.Read(serialized);
        ((int)deserialized["Count"]).ShouldBe(-42);
        ((int)deserialized["Offset"]).ShouldBe(0);
    }

    [Fact]
    public void RoundTrip_Int64()
    {
        var original = new VariantMap(
            new Dictionary<string, object> { ["BigValue"] = 1234567890123L }
        );
        byte[] serialized = original.Serialize();
        var deserialized = VariantMap.Read(serialized);
        ((long)deserialized["BigValue"]).ShouldBe(1234567890123L);
    }

    // ── Unsupported version ────────────────────────────────────────────

    [Fact]
    public void Read_Unsupported_Version_Throws()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x0200); // wrong version
        writer.Write((byte)0x00); // terminator
        ms.Position = 0;
        Should.Throw<FormatException>(() => VariantMap.Read(ms.ToArray()));
    }

    // ── TryGetValue / Dump ─────────────────────────────────────────────

    [Fact]
    public void TryGetValue_Finds_Existing_Key()
    {
        var map = new VariantMap(new Dictionary<string, object> { ["key"] = "val" });
        map.TryGetValue("key", out object? value).ShouldBeTrue();
        value.ShouldBe("val");
    }

    [Fact]
    public void TryGetValue_Missing_Key_Returns_False()
    {
        var map = new VariantMap([]);
        map.TryGetValue("nope", out _).ShouldBeFalse();
    }

    [Fact]
    public void Dump_Contains_Keys()
    {
        var map = new VariantMap(
            new Dictionary<string, object> { ["Name"] = "Test", ["Flag"] = true }
        );
        string dump = map.Dump();
        dump.ShouldContain("Name");
        dump.ShouldContain("Flag");
    }
}
