using System.Collections.Concurrent;

namespace KelliPhoto.Web.Services;

public interface IScanProgressService
{
    void StartScan(int folderId, int totalPhotos);
    void UpdateProgress(int folderId, int processedPhotos);
    void CompleteScan(int folderId);
    ScanProgress? GetProgress(int folderId);
    bool IsScanning(int folderId);
}

public class ScanProgressService : IScanProgressService
{
    private readonly ConcurrentDictionary<int, ScanProgress> _activeScans = new();
    private readonly ILogger<ScanProgressService> _logger;

    public ScanProgressService(ILogger<ScanProgressService> logger)
    {
        _logger = logger;
    }

    public void StartScan(int folderId, int totalPhotos)
    {
        _activeScans[folderId] = new ScanProgress
        {
            FolderId = folderId,
            TotalPhotos = totalPhotos,
            ProcessedPhotos = 0,
            StartTime = DateTime.UtcNow,
            IsComplete = false
        };
        _logger.LogInformation("Started scan progress tracking for folder {FolderId}, total photos: {Total}", folderId, totalPhotos);
    }

    public void UpdateProgress(int folderId, int processedPhotos)
    {
        if (_activeScans.TryGetValue(folderId, out var progress))
        {
            progress.ProcessedPhotos = processedPhotos;
            _logger.LogDebug("Scan progress for folder {FolderId}: {Processed}/{Total}", folderId, processedPhotos, progress.TotalPhotos);
        }
    }

    public void CompleteScan(int folderId)
    {
        if (_activeScans.TryGetValue(folderId, out var progress))
        {
            progress.IsComplete = true;
            progress.EndTime = DateTime.UtcNow;
            _logger.LogInformation("Completed scan for folder {FolderId}", folderId);
            
            // Remove after 30 seconds to allow clients to get final status
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                _activeScans.TryRemove(folderId, out _);
            });
        }
    }

    public ScanProgress? GetProgress(int folderId)
    {
        _activeScans.TryGetValue(folderId, out var progress);
        return progress;
    }

    public bool IsScanning(int folderId)
    {
        return _activeScans.TryGetValue(folderId, out var progress) && !progress.IsComplete;
    }
}

public class ScanProgress
{
    public int FolderId { get; set; }
    public int TotalPhotos { get; set; }
    public int ProcessedPhotos { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsComplete { get; set; }
    
    public double PercentComplete => TotalPhotos > 0 ? (double)ProcessedPhotos / TotalPhotos * 100 : 0;
    public TimeSpan Elapsed => (EndTime ?? DateTime.UtcNow) - StartTime;
}
