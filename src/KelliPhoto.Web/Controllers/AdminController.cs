using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using KelliPhoto.Web.Services;

namespace KelliPhoto.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IServiceProvider serviceProvider,
        ILogger<AdminController> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [HttpPost("scan/full")]
    public async Task<IActionResult> TriggerFullScan()
    {
        try
        {
            _logger.LogInformation("Full catalog scan triggered by admin user");
            
            // Get CatalogService from the service provider
            var catalogService = _serviceProvider.GetRequiredService<CatalogService>();
            await catalogService.TriggerFullScanAsync();
            
            return Ok(new { message = "Full catalog scan started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering full scan");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
