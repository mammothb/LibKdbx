namespace LibKdbx.Tests;

public class MergeModeTests
{
    [Fact]
    public void Defaults()
    {
        new Group().MergeMode.ShouldBe(MergeMode.Default);
    }

    [Fact]
    public void Clone_Preserves()
    {
        var group = new Group { Name = "Test", MergeMode = MergeMode.KeepNewer };
        Group clone = group.Clone();
        clone.MergeMode.ShouldBe(MergeMode.KeepNewer);
    }
}
