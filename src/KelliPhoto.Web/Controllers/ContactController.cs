using Microsoft.AspNetCore.Mvc;
using KelliPhoto.Web.Models;
using KelliPhoto.Web.Services;

namespace KelliPhoto.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly IContactFormService _contactFormService;
    private readonly IConfiguration _configuration;

    public ContactController(IContactFormService contactFormService, IConfiguration configuration)
    {
        _contactFormService = contactFormService;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitContactForm([FromBody] ContactFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Please check the form and try again.", errors = ModelState });
        }

        var result = await _contactFormService.SubmitAsync(model, GetClientIpAddress());

        if (result.Success)
        {
            return Ok(new { success = true, message = result.Message });
        }

        if (result.Message.Contains("Too many submissions", StringComparison.Ordinal))
        {
            return StatusCode(429, new { success = false, message = result.Message });
        }

        return StatusCode(500, new { success = false, message = result.Message });
    }

    private string GetClientIpAddress()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        // Prefer direct remote IP; trust X-Forwarded-* only when ForwardedHeaders:Enabled or behind local proxy.
        if (remoteIp is not null && !System.Net.IPAddress.IsLoopback(remoteIp))
        {
            return remoteIp.ToString();
        }

        if (ShouldTrustProxyHeaders())
        {
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(realIp))
            {
                return realIp;
            }
        }

        return remoteIp?.ToString() ?? "unknown";
    }

    private bool ShouldTrustProxyHeaders() =>
        _configuration.GetValue<bool>("ForwardedHeaders:Enabled")
        || HttpContext.Connection.RemoteIpAddress is null
        || System.Net.IPAddress.IsLoopback(HttpContext.Connection.RemoteIpAddress);
}
