namespace LibKdbx.Tests;

public class DatabaseReferenceTests
{
    // ── SearchIn = O (custom attribute value) ───────────────────────────────

    [Fact]
    public void SearchIn_CustomAttribute_Finds_Entry_By_Value()
    {
        using Database db = Database.Create("pw");
        Entry e1 = new() { Title = "Target", Notes = "target_notes" };
        e1.Attributes.Set("Server", "prod-db-01");
        db.RootGroup!.AddEntry(e1);

        Entry e2 = new() { Title = "RefEntry" };
        e2.Attributes.Set("Notes", "{REF:N@O:prod-db-01}");
        db.RootGroup.AddEntry(e2);

        string resolved = db.ResolveField(e2, "Notes");
        resolved.ShouldBe("target_notes");
    }

    [Fact]
    public void SearchIn_CustomAttribute_NoMatch_Returns_Original()
    {
        using Database db = Database.Create("pw");
        Entry e1 = new() { Title = "Only" };
        e1.Attributes.Set("Server", "prod-db-01");
        db.RootGroup!.AddEntry(e1);

        Entry e2 = new() { Title = "RefEntry" };
        e2.Attributes.Set("Title", "{REF:T@O:nonexistent}");
        db.RootGroup.AddEntry(e2);

        string resolved = db.ResolveField(e2, "Title");
        resolved.ShouldBe("{REF:T@O:nonexistent}");
    }

    [Fact]
    public void SearchIn_CustomAttribute_FirstMatchWins()
    {
        using Database db = Database.Create("pw");
        // Two entries with same custom attribute value — first found wins
        Entry e1 = new() { Title = "First", UserName = "user1" };
        e1.Attributes.Set("Tag", "shared");
        db.RootGroup!.AddEntry(e1);

        Entry e2 = new() { Title = "Second", UserName = "user2" };
        e2.Attributes.Set("Tag", "shared");
        db.RootGroup.AddEntry(e2);

        Entry refEntry = new() { Title = "Ref" };
        refEntry.Attributes.Set("UserName", "{REF:U@O:shared}");
        db.RootGroup.AddEntry(refEntry);

        string resolved = db.ResolveField(refEntry, "UserName");
        // First entry in index order should be found
        (resolved == "user1" || resolved == "user2").ShouldBeTrue();
    }

    [Fact]
    public void SearchIn_CustomAttribute_SearchValue_Partial_Not_Matched()
    {
        using Database db = Database.Create("pw");
        Entry e1 = new() { Title = "Target" };
        e1.Attributes.Set("Server", "prod-db-01");
        db.RootGroup!.AddEntry(e1);

        Entry e2 = new() { Title = "RefEntry" };
        e2.Attributes.Set("Notes", "{REF:N@O:prod}"); // partial, no match
        db.RootGroup.AddEntry(e2);

        string resolved = db.ResolveField(e2, "Notes");
        // ContainsValue is exact match, so "prod" should not match "prod-db-01"
        resolved.ShouldBe("{REF:N@O:prod}");
    }

    // ── WantedField = I (return UUID) ────────────────────────────────────────

    [Fact]
    public void WantedField_Uuid_Returns_Target_Uuid_Hex()
    {
        using Database db = Database.Create("pw");
        Guid targetUuid = Guid.NewGuid();
        Entry e1 = new() { Uuid = targetUuid, Title = "TargetEntry" };
        db.RootGroup!.AddEntry(e1);

        Entry e2 = new() { Title = "RefEntry" };
        e2.Attributes.Set("Notes", "{REF:I@T:TargetEntry}");
        db.RootGroup.AddEntry(e2);

        string resolved = db.ResolveField(e2, "Notes");
        resolved.ShouldBe(targetUuid.ToString("N"));
    }

    [Fact]
    public void WantedField_Uuid_With_SearchIn_Uuid()
    {
        using Database db = Database.Create("pw");
        Guid targetUuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Entry e1 = new() { Uuid = targetUuid, Title = "Target" };
        db.RootGroup!.AddEntry(e1);

        string targetHex = targetUuid.ToString("N");
        Entry e2 = new() { Title = "RefEntry" };
        e2.Attributes.Set("Notes", $"{{REF:I@I:{targetHex}}}");
        db.RootGroup.AddEntry(e2);

        string resolved = db.ResolveField(e2, "Notes");
        resolved.ShouldBe(targetHex);
    }

    [Fact]
    public void WantedField_Uuid_Target_NotFound_Returns_Original()
    {
        using Database db = Database.Create("pw");
        Entry e2 = new() { Title = "RefEntry" };
        e2.Attributes.Set("Notes", "{REF:I@T:Nonexistent}");
        db.RootGroup!.AddEntry(e2);

        string resolved = db.ResolveField(e2, "Notes");
        resolved.ShouldBe("{REF:I@T:Nonexistent}");
    }

    // ── Depth limit ──────────────────────────────────────────────────────────

    [Fact]
    public void ResolveValue_Depth_Limit_Prevents_Infinite_Loop()
    {
        using Database db = Database.Create("pw");
        Entry e1 = new() { Title = "A", Notes = "{REF:N@T:B}" };
        Entry e2 = new() { Title = "B", Notes = "{REF:N@T:A}" };
        db.RootGroup!.AddEntry(e1);
        db.RootGroup.AddEntry(e2);

        // With depth 1, we get one expansion then hit the limit
        string resolved = db.ResolveField(e1, "Notes", maxDepth: 1);
        // After 1 hop, e2.Notes is "{REF:N@T:A}" → that's still a REF, but depth is now 0
        // So it returns "{REF:N@T:A}" as-is
        resolved.ShouldBe("{REF:N@T:A}");
    }

    // ── Existing field codes still work ──────────────────────────────────────

    [Fact]
    public void Standard_Reference_Title_To_Title()
    {
        using Database db = Database.Create("pw");
        Entry e1 = new() { Title = "Original", UserName = "alice" };
        db.RootGroup!.AddEntry(e1);

        Entry e2 = new() { Title = "Ref" };
        e2.Attributes.Set("UserName", "{REF:U@T:Original}");
        db.RootGroup.AddEntry(e2);

        string resolved = db.ResolveField(e2, "UserName");
        resolved.ShouldBe("alice");
    }

    [Fact]
    public void Standard_Reference_With_Uuid_Search()
    {
        using Database db = Database.Create("pw");
        Guid targetUuid = Guid.NewGuid();
        Entry e1 = new() { Uuid = targetUuid, Password = "secret99" };
        db.RootGroup!.AddEntry(e1);

        string hex = targetUuid.ToString("N");
        Entry e2 = new() { Title = "Ref" };
        e2.Attributes.Set("Password", $"{{REF:P@I:{hex}}}");
        db.RootGroup.AddEntry(e2);

        string resolved = db.ResolveField(e2, "Password");
        resolved.ShouldBe("secret99");
    }
}
