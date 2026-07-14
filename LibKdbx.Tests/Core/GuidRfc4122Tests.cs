namespace LibKdbx.Tests;

public class GuidRfc4122Tests
{
    [Fact]
    public void Convert_RoundTrip()
    {
        Guid original = Guid.NewGuid();
        byte[] bytes = GuidRfc4122.ToBytes(original);
        Guid back = GuidRfc4122.FromBytes(bytes);
        back.ShouldBe(original);
    }

    [Fact]
    public void Known_Guid_Produces_Known_Bytes()
    {
        // Null GUID → all zeros
        byte[] bytes = GuidRfc4122.ToBytes(Guid.Empty);
        bytes.ShouldBe(new byte[16]); // all zero
    }
}
