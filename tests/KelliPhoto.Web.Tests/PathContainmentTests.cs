using KelliPhoto.Web.Services;
using Microsoft.Extensions.Configuration;

namespace KelliPhoto.Web.Tests;

public class PathContainmentTests
{
    [Fact]
    public void EnsureUnderGalleryRoot_AllowsChildPath()
    {
        var gallery = Path.Combine(Path.GetTempPath(), "gal-" + Guid.NewGuid());
        Directory.CreateDirectory(gallery);
        try
        {
            var ps = CreatePathService(gallery);
            var child = Path.Combine(gallery, "albums", "a");
            Assert.Equal(Path.GetFullPath(child), ps.EnsureUnderGalleryRoot(child));
        }
        finally { Directory.Delete(gallery, true); }
    }

    [Fact]
    public void EnsureUnderGalleryRoot_RejectsEscape()
    {
        var gallery = Path.Combine(Path.GetTempPath(), "gal-" + Guid.NewGuid());
        Directory.CreateDirectory(gallery);
        try
        {
            var ps = CreatePathService(gallery);
            Assert.Throws<InvalidOperationException>(() =>
                ps.EnsureUnderGalleryRoot(Path.Combine(gallery, "..", "outside")));
        }
        finally { Directory.Delete(gallery, true); }
    }

    private static IPathService CreatePathService(string galleryPath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GallerySettings:GalleryPath"] = galleryPath
            }).Build();
        return new PathService(config);
    }
}
