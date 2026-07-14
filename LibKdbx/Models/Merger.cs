namespace LibKdbx;

/// <summary>
/// Merges a source database into a target database in-place.
/// Matches groups and entries by UUID, resolves conflicts per <see cref="MergeMode"/>,
/// merges history, deletions, and metadata.
/// </summary>
public static class Merger
{
    /// <summary>
    /// Merges <paramref name="source"/> into <paramref name="target"/> in-place.
    /// If <paramref name="dryRun"/> is true, no changes are written to target.
    /// </summary>
    public static void Merge(
        Database source,
        Database target,
        MergeMode defaultMode = MergeMode.Default,
        bool dryRun = false
    )
    {
        if (source.RootGroup is null || target.RootGroup is null)
        {
            throw new InvalidOperationException("Both databases must have a root group.");
        }

        MergeGroup(source.RootGroup, target.RootGroup, defaultMode, dryRun);
        MergeDeletions(source, target, defaultMode, dryRun);
        MergeMetadata(source, target, dryRun);

        if (!dryRun)
        {
            target.SetChanged();
        }
    }

    // ── Group merge ───────────────────────────────────────────────────────

    private static void MergeGroup(
        Group sourceGroup,
        Group targetGroup,
        MergeMode defaultMode,
        bool dryRun
    )
    {
        MergeMode mode = defaultMode == MergeMode.Default ? targetGroup.MergeMode : defaultMode;

        // Merge entries
        List<Entry> sourceEntries = [.. sourceGroup.Entries];
        foreach (Entry sourceEntry in sourceEntries)
        {
            Entry? targetEntry = FindEntryByUuid(sourceEntry.Uuid, targetGroup);

            if (targetEntry is null)
            {
                if (!dryRun)
                {
                    Entry clone = CloneEntry(sourceEntry);
                    targetGroup.AddEntry(clone);
                }
            }
            else
            {
                MergeEntry(sourceEntry, targetEntry, mode, dryRun);
            }
        }

        // Merge subgroups recursively
        List<Group> sourceChildren = [.. sourceGroup.Groups];
        foreach (Group sourceChild in sourceChildren)
        {
            Group? targetChild = FindGroupByUuid(sourceChild.Uuid, targetGroup);

            if (targetChild is null)
            {
                if (!dryRun)
                {
                    Group clone = CloneGroup(sourceChild, true);
                    targetGroup.AddGroup(clone);
                }
                // Clone already copied all children — no need to recurse
            }
            else
            {
                MergeGroupConflict(sourceChild, targetChild, dryRun);
                MergeGroup(sourceChild, targetChild, defaultMode, dryRun);
            }
        }
    }

    // ── Entry merge ───────────────────────────────────────────────────────

    private static void MergeEntry(Entry source, Entry target, MergeMode mode, bool dryRun)
    {
        DateTime targetTime = TruncateToSeconds(target.Times.LastModificationTime);
        DateTime sourceTime = TruncateToSeconds(source.Times.LastModificationTime);

        bool sourceNewer = sourceTime > targetTime;

        if (!sourceNewer && mode != MergeMode.Synchronize)
        {
            MergeHistory(source, target, null, sourceTime, targetTime, dryRun);
            return;
        }

        // Capture target state before modification (for history)
        Entry? oldTarget = null;
        if (!dryRun)
        {
            oldTarget = target.Clone();
            oldTarget.Uuid = target.Uuid;
        }

        if (!dryRun)
        {
            target.IconId = source.IconId;
            target.CustomIconUuid = source.CustomIconUuid;
            target.ForegroundColor = source.ForegroundColor;
            target.BackgroundColor = source.BackgroundColor;
            target.OverrideUrl = source.OverrideUrl;
            target.Tags = source.Tags;
            target.ExcludeFromReports = source.ExcludeFromReports;

            target.Title = source.Title;
            target.UserName = source.UserName;
            target.Password = source.Password;
            target.Url = source.Url;
            target.Notes = source.Notes;

            // Merge custom attributes: source overwrites matching keys
            foreach (string key in source.Attributes.CustomKeys)
            {
                string? val = source.Attributes.Get(key);
                if (val is not null)
                {
                    target.Attributes.Set(key, val);
                }
            }

            // In Synchronize mode, remove target keys not in source
            if (mode == MergeMode.Synchronize)
            {
                List<string> targetKeys = [.. target.Attributes.CustomKeys];
                foreach (string key in targetKeys)
                {
                    if (!source.Attributes.Contains(key))
                    {
                        target.Attributes.Remove(key);
                    }
                }
            }

            // Merge attachments
            foreach (string key in source.Attachments.Keys)
            {
                byte[]? data = source.Attachments.Get(key);
                if (data is not null)
                {
                    target.Attachments.Set(key, data);
                }
            }

            if (mode == MergeMode.Synchronize)
            {
                List<string> targetNames = [.. target.Attachments.Keys];
                foreach (string key in targetNames)
                {
                    if (!source.Attachments.Contains(key))
                    {
                        target.Attachments.Remove(key);
                    }
                }
            }

            // AutoType
            target.AutoType.Enabled = source.AutoType.Enabled;
            target.AutoType.DataTransferObfuscation = source.AutoType.DataTransferObfuscation;
            target.AutoType.DefaultSequence = source.AutoType.DefaultSequence;
            target.AutoType.Associations.Clear();
            foreach (AutoTypeAssociation assoc in source.AutoType.Associations)
            {
                target.AutoType.Associations.Add(
                    new AutoTypeAssociation { Window = assoc.Window, Sequence = assoc.Sequence }
                );
            }

            // CustomData
            if (source.CustomData is not null)
            {
                target.CustomData ??= new CustomData();
                foreach (string key in source.CustomData.Keys)
                {
                    string? val = source.CustomData.GetValue(key);
                    if (val is not null)
                    {
                        target.CustomData.Set(key, val);
                    }
                }
            }

            // Times
            DateTime locationChanged = target.Times.LocationChanged;
            target.Times.ExpiryTime = source.Times.ExpiryTime;
            target.Times.Expires = source.Times.Expires;
            target.Times.LastModificationTime = source.Times.LastModificationTime;
            target.Times.LocationChanged =
                locationChanged > source.Times.LocationChanged
                    ? locationChanged
                    : source.Times.LocationChanged;
        }

        MergeHistory(source, target, oldTarget, sourceTime, targetTime, dryRun);
    }

    // ── Group conflict ────────────────────────────────────────────────────

    private static void MergeGroupConflict(Group source, Group target, bool dryRun)
    {
        DateTime targetTime = TruncateToSeconds(target.Times.LastModificationTime);
        DateTime sourceTime = TruncateToSeconds(source.Times.LastModificationTime);

        if (sourceTime <= targetTime)
        {
            return;
        }

        if (!dryRun)
        {
            target.Name = source.Name;
            target.Notes = source.Notes;
            target.IconId = source.IconId;
            target.CustomIconUuid = source.CustomIconUuid;
            target.IsExpanded = source.IsExpanded;
            target.EnableAutoType = source.EnableAutoType;
            target.EnableSearching = source.EnableSearching;
            target.Tags = source.Tags;
            target.DefaultAutoTypeSequence = source.DefaultAutoTypeSequence;

            target.Times.ExpiryTime = source.Times.ExpiryTime;
            target.Times.Expires = source.Times.Expires;
            target.Times.LastModificationTime = source.Times.LastModificationTime;
        }
    }

    // ── History merge ─────────────────────────────────────────────────────

    private static void MergeHistory(
        Entry source,
        Entry target,
        Entry? oldTarget,
        DateTime sourceModTime,
        DateTime targetModTime,
        bool dryRun
    )
    {
        if (dryRun)
        {
            return;
        }

        int maxItems =
            target.Database?.Metadata?.HistoryMaxItems
            ?? source.Database?.Metadata?.HistoryMaxItems
            ?? 10;

        // Collect all history items (allow timestamp collisions)
        List<Entry> merged = [];

        merged.AddRange(target.History);
        foreach (Entry h in source.History)
        {
            merged.Add(CloneEntry(h));
        }

        // Add old target as history item when source is newer
        DateTime sourceTs = TruncateToSeconds(sourceModTime);
        DateTime targetTs = TruncateToSeconds(targetModTime);

        if (sourceTs > targetTs && oldTarget is not null)
        {
            merged.Add(oldTarget);
        }
        else if (targetTs > sourceTs)
        {
            merged.Add(CloneEntry(source));
        }

        // Sort by modification time, keep newest maxItems
        List<Entry> sorted = [.. merged.OrderBy(e => e.Times.LastModificationTime)];

        target.History.Clear();
        int skip = Math.Max(0, sorted.Count - maxItems);
        for (int i = skip; i < sorted.Count; i++)
        {
            target.History.Add(sorted[i]);
        }
    }

    // ── Deletions merge ───────────────────────────────────────────────────

    private static void MergeDeletions(
        Database source,
        Database target,
        MergeMode defaultMode,
        bool dryRun
    )
    {
        if (defaultMode != MergeMode.Synchronize)
        {
            return;
        }

        if (target.RootGroup is null)
        {
            return;
        }

        HashSet<Guid> sourceDeletionUuids = [.. source.DeletedObjects.Select(d => d.Uuid)];

        // Find and remove deleted entries
        List<Entry> allEntries = [.. target.RootGroup.FindAllEntries(_ => true)];
        foreach (Entry entry in allEntries)
        {
            if (sourceDeletionUuids.Contains(entry.Uuid))
            {
                if (!dryRun && entry.ParentGroup is not null)
                {
                    entry.ParentGroup.RemoveEntry(entry);
                }
            }
        }

        // Find and remove deleted groups (only empty ones)
        List<Group> allGroups = target.RootGroup.GroupsRecursive(true);
        foreach (Group group in allGroups)
        {
            if (
                sourceDeletionUuids.Contains(group.Uuid)
                && group.Groups.Count == 0
                && group.Entries.Count == 0
                && group.ParentGroup is not null
            )
            {
                if (!dryRun)
                {
                    group.ParentGroup.RemoveGroup(group);
                }
            }
        }

        // Union DeletedObjects into target
        if (!dryRun)
        {
            HashSet<Guid> targetDeletionUuids = [.. target.DeletedObjects.Select(d => d.Uuid)];
            foreach (DeletedObject obj in source.DeletedObjects)
            {
                if (!targetDeletionUuids.Contains(obj.Uuid))
                {
                    target.DeletedObjects.Add(obj);
                }
            }
        }
    }

    // ── Metadata merge ────────────────────────────────────────────────────

    private static void MergeMetadata(Database source, Database target, bool dryRun)
    {
        if (dryRun || source.Metadata is null || target.Metadata is null)
        {
            return;
        }

        MergeCustomData(source.Metadata.CustomData, target.Metadata.CustomData);
    }

    private static void MergeCustomData(CustomData? source, CustomData? target)
    {
        if (source is null || target is null)
        {
            return;
        }

        // Copy keys from source that don't exist in target, or have different values
        foreach (string key in source.Keys)
        {
            string? sourceVal = source.GetValue(key);
            string? targetVal = target.GetValue(key);
            if (sourceVal is not null && sourceVal != targetVal)
            {
                target.Set(key, sourceVal);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Entry? FindEntryByUuid(Guid uuid, Group root)
    {
        if (root.Database is not null)
        {
            return root.Database.FindEntryByUuid(uuid);
        }

        return root.FindEntry(e => e.Uuid == uuid);
    }

    private static Group? FindGroupByUuid(Guid uuid, Group root)
    {
        Group? found = null;
        Queue<Group> queue = new([root]);
        while (queue.Count > 0)
        {
            Group g = queue.Dequeue();
            if (g.Uuid == uuid)
            {
                found = g;
                break;
            }
            foreach (Group child in g.Groups)
            {
                queue.Enqueue(child);
            }
        }
        return found;
    }

    private static Entry CloneEntry(Entry source)
    {
        Entry clone = source.Clone();
        clone.Uuid = source.Uuid;
        return clone;
    }

    private static Group CloneGroup(Group source, bool preserveUuid = false)
    {
        Group clone = new()
        {
            Uuid = preserveUuid ? source.Uuid : Guid.NewGuid(),
            Name = source.Name,
            Notes = source.Notes,
            IconId = source.IconId,
            CustomIconUuid = source.CustomIconUuid,
            IsExpanded = source.IsExpanded,
            EnableAutoType = source.EnableAutoType,
            EnableSearching = source.EnableSearching,
            Tags = source.Tags,
            DefaultAutoTypeSequence = source.DefaultAutoTypeSequence,
            LastTopVisibleEntry = source.LastTopVisibleEntry,
            PreviousParentGroup = source.PreviousParentGroup,
            MergeMode = source.MergeMode,
            Times = source.Times.Clone(),
        };
        if (source.CustomData is not null)
        {
            clone.CustomData = new CustomData();
            clone.CustomData.CopyFrom(source.CustomData);
        }
        foreach (Entry entry in source.Entries)
        {
            Entry entryClone = entry.Clone();
            entryClone.Uuid = entry.Uuid;
            clone.AddEntry(entryClone);
        }
        foreach (Group child in source.Groups)
        {
            clone.AddGroup(CloneGroup(child, preserveUuid));
        }
        return clone;
    }

    private static DateTime TruncateToSeconds(DateTime dt)
    {
        return dt.AddTicks(-(dt.Ticks % TimeSpan.TicksPerSecond));
    }
}
