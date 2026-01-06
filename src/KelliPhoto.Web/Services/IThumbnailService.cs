namespace KelliPhoto.Web.Services;

public interface IThumbnailService
{
    Task<string> GetOrCreateThumbnailAsync(int photoId, int size = 300);
    Task<Stream> GetThumbnailStreamAsync(int photoId, int size = 300);
    Task DeleteThumbnailAsync(int photoId, int size);
}
