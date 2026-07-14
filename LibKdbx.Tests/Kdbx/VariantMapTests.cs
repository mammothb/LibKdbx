namespace LibKdbx.Tests;

public class VariantMapTests
{
    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var original = new VariantMap(new Dictionary<string, object>
        {
            ["$UUID"] = GuidRfc4122.ToBytes(Guid.NewGuid()),
            ["S"] = new byte[] { 1, 2, 3, 4, 5 },
            ["P"] = (uint)4,
            ["M"] = (ulong)1024 * 1024 * 64,
            ["I"] = (ulong)3,
            ["V"] = (uint)0x13,
            ["Name"] = "TestDB",
            ["Flag"] = true,
        });

        byte[] serialized = original.Serialize();
        var deserialized = VariantMap.Read(serialized);

        ((uint)deserialized["P"]).ShouldBe((uint)4);
        ((ulong)deserialized["M"]).ShouldBe((ulong)1024 * 1024 * 64);
        ((ulong)deserialized["I"]).ShouldBe((ulong)3);
        ((string)deserialized["Name"]).ShouldBe("TestDB");
        ((bool)deserialized["Flag"]).ShouldBe(true);
    }
}
