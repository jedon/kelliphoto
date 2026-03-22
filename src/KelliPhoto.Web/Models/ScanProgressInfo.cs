namespace KelliPhoto.Web.Models;

public class ScanProgressInfo
{
    public int FolderId { get; set; }
    public int TotalPhotos { get; set; }
    public int ProcessedPhotos { get; set; }
    public double PercentComplete { get; set; }
    public bool IsComplete { get; set; }
    public double ElapsedSeconds { get; set; }
}
