using KelliPhoto.Web.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KelliPhoto.Web.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Folder> Folders { get; set; }
    public DbSet<Photo> Photos { get; set; }
    public DbSet<Thumbnail> Thumbnails { get; set; }
    public DbSet<FolderCoverPhoto> FolderCoverPhotos { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<FolderTag> FolderTags { get; set; }
    public DbSet<PhotoTag> PhotoTags { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Folder relationships
        builder.Entity<Folder>()
            .HasOne(f => f.Parent)
            .WithMany(f => f.Children)
            .HasForeignKey(f => f.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Folder>()
            .HasOne(f => f.ThumbnailPhoto)
            .WithMany()
            .HasForeignKey(f => f.ThumbnailPhotoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Folder>()
            .HasIndex(f => f.Path)
            .IsUnique();

        // Photo relationships
        builder.Entity<Photo>()
            .HasOne(p => p.Folder)
            .WithMany(f => f.Photos)
            .HasForeignKey(p => p.FolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Photo>()
            .HasIndex(p => p.FilePath)
            .IsUnique();

        // Thumbnail relationships
        builder.Entity<Thumbnail>()
            .HasOne(t => t.Photo)
            .WithMany(p => p.Thumbnails)
            .HasForeignKey(t => t.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Thumbnail>()
            .HasIndex(t => new { t.PhotoId, t.Size })
            .IsUnique();

        builder.Entity<FolderCoverPhoto>()
            .HasKey(fcp => new { fcp.FolderId, fcp.PhotoId });

        builder.Entity<FolderCoverPhoto>()
            .HasIndex(fcp => new { fcp.FolderId, fcp.SortOrder })
            .IsUnique();

        builder.Entity<FolderCoverPhoto>()
            .HasOne(fcp => fcp.Folder)
            .WithMany()
            .HasForeignKey(fcp => fcp.FolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<FolderCoverPhoto>()
            .HasOne(fcp => fcp.Photo)
            .WithMany()
            .HasForeignKey(fcp => fcp.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Tag>()
            .HasIndex(t => t.NameNormalized)
            .IsUnique();

        builder.Entity<FolderTag>()
            .HasKey(ft => new { ft.FolderId, ft.TagId });

        builder.Entity<FolderTag>()
            .HasOne(ft => ft.Folder)
            .WithMany()
            .HasForeignKey(ft => ft.FolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<FolderTag>()
            .HasOne(ft => ft.Tag)
            .WithMany(t => t.FolderTags)
            .HasForeignKey(ft => ft.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PhotoTag>()
            .HasKey(pt => new { pt.PhotoId, pt.TagId });

        builder.Entity<PhotoTag>()
            .HasOne(pt => pt.Photo)
            .WithMany()
            .HasForeignKey(pt => pt.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PhotoTag>()
            .HasOne(pt => pt.Tag)
            .WithMany(t => t.PhotoTags)
            .HasForeignKey(pt => pt.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
