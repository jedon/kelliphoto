using Microsoft.AspNetCore.Mvc;
using KelliPhoto.Web.Services;

namespace KelliPhoto.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScanProgressController : ControllerBase
{
    private readonly IScanProgressService _progressService;
    private readonly ILogger<ScanProgressController> _logger;

    public ScanProgressController(
        IScanProgressService progressService,
        ILogger<ScanProgressController> logger)
    {
        _progressService = progressService;
        _logger = logger;
    }

    [HttpGet("{folderId}")]
    public IActionResult GetProgress(int folderId)
    {
        var progress = _progressService.GetProgress(folderId);
        if (progress == null)
        {
            return NotFound(new { message = "No scan in progress for this folder" });
        }

        return Ok(new
        {
            folderId = progress.FolderId,
            totalPhotos = progress.TotalPhotos,
            processedPhotos = progress.ProcessedPhotos,
            percentComplete = progress.PercentComplete,
            isComplete = progress.IsComplete,
            elapsedSeconds = progress.Elapsed.TotalSeconds
        });
    }
}
