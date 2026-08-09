using KelliPhoto.Web.Data;
using KelliPhoto.Web.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace KelliPhoto.Web.Services;

public class TagService : ITagService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public TagService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Tag> EnsureTagAsync(string name, string? group = null)
    {
        var displayName = NormalizeDisplayName(name);
        var nameNormalized = ToNameNormalized(displayName);
        await using var context = await _contextFactory.CreateDbContextAsync();

        var existing = await FindTagByNameAsync(context, nameNormalized);
        if (existing is not null)
        {
            if (group is not null && existing.Group != group)
            {
                existing.Group = string.IsNullOrWhiteSpace(group) ? null : group.Trim();
                await context.SaveChangesAsync();
            }

            return existing;
        }

        var tag = new Tag
        {
            Name = displayName,
            NameNormalized = nameNormalized,
            Group = string.IsNullOrWhiteSpace(group) ? null : group.Trim()
        };
        context.Tags.Add(tag);

        try
        {
            await context.SaveChangesAsync();
            return tag;
        }
        catch (DbUpdateException)
        {
            // Concurrent EnsureTagAsync raced on the unique NameNormalized index.
            context.Entry(tag).State = EntityState.Detached;
            var raced = await FindTagByNameAsync(context, nameNormalized);
            if (raced is null)
                throw;

            if (group is not null && raced.Group != group)
            {
                raced.Group = string.IsNullOrWhiteSpace(group) ? null : group.Trim();
                await context.SaveChangesAsync();
            }

            return raced;
        }
    }

    public async Task<List<Tag>> AutocompleteAsync(string prefix, int take = 20)
    {
        if (take <= 0)
            return [];

        var trimmed = (prefix ?? string.Empty).Trim();
        await using var context = await _contextFactory.CreateDbContextAsync();

        var lower = trimmed.ToLowerInvariant();
        var query = context.Tags.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(trimmed))
            query = query.Where(t => t.NameNormalized.StartsWith(lower));

        return await query
            .OrderBy(t => t.Name)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Tag>> GetTagsForFolderAsync(int folderId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FolderTags
            .AsNoTracking()
            .Where(ft => ft.FolderId == folderId)
            .Select(ft => ft.Tag)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<Tag>> GetTagsForPhotoAsync(int photoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PhotoTags
            .AsNoTracking()
            .Where(pt => pt.PhotoId == photoId)
            .Select(pt => pt.Tag)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task AttachToFolderAsync(int folderId, int tagId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var exists = await context.FolderTags
            .AnyAsync(ft => ft.FolderId == folderId && ft.TagId == tagId);
        if (exists)
            return;

        context.FolderTags.Add(new FolderTag { FolderId = folderId, TagId = tagId });
        await context.SaveChangesAsync();
    }

    public async Task DetachFromFolderAsync(int folderId, int tagId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var link = await context.FolderTags
            .FirstOrDefaultAsync(ft => ft.FolderId == folderId && ft.TagId == tagId);
        if (link is null)
            return;

        context.FolderTags.Remove(link);
        await context.SaveChangesAsync();
    }

    public async Task AttachToPhotoAsync(int photoId, int tagId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var exists = await context.PhotoTags
            .AnyAsync(pt => pt.PhotoId == photoId && pt.TagId == tagId);
        if (exists)
            return;

        context.PhotoTags.Add(new PhotoTag { PhotoId = photoId, TagId = tagId });
        await context.SaveChangesAsync();
    }

    public async Task DetachFromPhotoAsync(int photoId, int tagId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var link = await context.PhotoTags
            .FirstOrDefaultAsync(pt => pt.PhotoId == photoId && pt.TagId == tagId);
        if (link is null)
            return;

        context.PhotoTags.Remove(link);
        await context.SaveChangesAsync();
    }

    public async Task BulkAttachToFoldersAsync(IReadOnlyList<int> folderIds, IReadOnlyList<string> tagNames)
    {
        if (folderIds.Count == 0 || tagNames.Count == 0)
            return;

        var tags = new List<Tag>();
        foreach (var name in tagNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            tags.Add(await EnsureTagAsync(name));
        }

        if (tags.Count == 0)
            return;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var distinctFolderIds = folderIds.Distinct().ToList();
        var tagIds = tags.Select(t => t.Id).Distinct().ToList();

        var existing = await context.FolderTags
            .Where(ft => distinctFolderIds.Contains(ft.FolderId) && tagIds.Contains(ft.TagId))
            .Select(ft => new { ft.FolderId, ft.TagId })
            .ToListAsync();
        var existingSet = existing.Select(e => (e.FolderId, e.TagId)).ToHashSet();

        foreach (var folderId in distinctFolderIds)
        {
            foreach (var tagId in tagIds)
            {
                if (existingSet.Contains((folderId, tagId)))
                    continue;
                context.FolderTags.Add(new FolderTag { FolderId = folderId, TagId = tagId });
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task BulkDetachFromFoldersAsync(IReadOnlyList<int> folderIds, IReadOnlyList<string> tagNames)
    {
        if (folderIds.Count == 0 || tagNames.Count == 0)
            return;

        var normalizedNames = tagNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => ToNameNormalized(NormalizeDisplayName(n)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (normalizedNames.Count == 0)
            return;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var distinctFolderIds = folderIds.Distinct().ToList();
        var nameSet = normalizedNames.ToHashSet(StringComparer.Ordinal);

        var links = await context.FolderTags
            .Include(ft => ft.Tag)
            .Where(ft => distinctFolderIds.Contains(ft.FolderId))
            .ToListAsync();

        var toRemove = links
            .Where(ft => nameSet.Contains(ft.Tag.NameNormalized))
            .ToList();
        if (toRemove.Count == 0)
            return;

        context.FolderTags.RemoveRange(toRemove);
        await context.SaveChangesAsync();
    }

    public IReadOnlyList<string> ListSuggestedGroups() => TagGroups.Suggested;

    private static string NormalizeDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required.", nameof(name));
        return name.Trim();
    }

    private static string ToNameNormalized(string displayName) =>
        displayName.Trim().ToLowerInvariant();

    private static async Task<Tag?> FindTagByNameAsync(ApplicationDbContext context, string nameNormalized)
    {
        return await context.Tags
            .FirstOrDefaultAsync(t => t.NameNormalized == nameNormalized);
    }
}
