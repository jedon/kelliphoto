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
    }
}
