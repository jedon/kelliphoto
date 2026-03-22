namespace KelliPhoto.Web.Services;

public interface IWebImageService
{
    Task<Stream> GetWebImageStreamAsync(
        int photoId,
        int maxDimension = 2000,
        bool watermark = true,
        CancellationToken cancellationToken = default);
}

