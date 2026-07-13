namespace Mmb.KeePass.Tests;

public class DeletedObjectTests
{
    [Fact]
    public void DeletedObject_Constructor_SetsProperties()
    {
        var uuid = Guid.NewGuid();
        var time = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        var del = new DeletedObject(uuid, time);

        del.Uuid.ShouldBe(uuid);
        del.DeletionTime.ShouldBe(time);
    }

    [Fact]
    public void DeletedObject_Equality_SameValues_AreEqual()
    {
        var uuid = Guid.NewGuid();
        DateTime time = DateTime.UtcNow;

        var a = new DeletedObject(uuid, time);
        var b = new DeletedObject(uuid, time);

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void DeletedObject_Equality_DifferentUuid_AreNotEqual()
    {
        DateTime time = DateTime.UtcNow;
        var a = new DeletedObject(Guid.NewGuid(), time);
        var b = new DeletedObject(Guid.NewGuid(), time);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void DeletedObject_Equality_DifferentTime_AreNotEqual()
    {
        var uuid = Guid.NewGuid();
        var a = new DeletedObject(uuid, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var b = new DeletedObject(uuid, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        a.ShouldNotBe(b);
    }

    [Fact]
    public void DeletedObject_Default_HasEmptyUuid()
    {
        default(DeletedObject).Uuid.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void DeletedObject_Default_HasMinDateTime()
    {
        default(DeletedObject).DeletionTime.ShouldBe(default);
    }

    [Fact]
    public void DeletedObject_WithExpression_RetainsOtherProperty()
    {
        var original = new DeletedObject(Guid.NewGuid(), DateTime.UtcNow);
        DeletedObject modified = original with { DeletionTime = DateTime.MinValue };

        modified.Uuid.ShouldBe(original.Uuid);
        modified.DeletionTime.ShouldBe(DateTime.MinValue);
    }
}
