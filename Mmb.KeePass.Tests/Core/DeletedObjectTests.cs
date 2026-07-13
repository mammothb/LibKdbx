namespace Mmb.KeePass.Tests;

public class DeletedObjectTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        Guid uuid = Guid.NewGuid();
        DateTime time = new(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        DeletedObject del = new(uuid, time);

        del.Uuid.ShouldBe(uuid);
        del.DeletionTime.ShouldBe(time);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        Guid uuid = Guid.NewGuid();
        DateTime time = DateTime.UtcNow;

        DeletedObject a = new(uuid, time);
        DeletedObject b = new(uuid, time);

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentUuid_AreNotEqual()
    {
        DateTime time = DateTime.UtcNow;
        DeletedObject a = new(Guid.NewGuid(), time);
        DeletedObject b = new(Guid.NewGuid(), time);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentTime_AreNotEqual()
    {
        Guid uuid = Guid.NewGuid();
        DeletedObject a = new(uuid, new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        DeletedObject b = new(uuid, new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Default_HasEmptyUuid()
    {
        default(DeletedObject).Uuid.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Default_HasMinDateTime()
    {
        default(DeletedObject).DeletionTime.ShouldBe(default);
    }

    [Fact]
    public void WithExpression_RetainsOtherProperty()
    {
        DeletedObject original = new(Guid.NewGuid(), DateTime.UtcNow);
        DeletedObject modified = original with { DeletionTime = DateTime.MinValue };

        modified.Uuid.ShouldBe(original.Uuid);
        modified.DeletionTime.ShouldBe(DateTime.MinValue);
    }
}
