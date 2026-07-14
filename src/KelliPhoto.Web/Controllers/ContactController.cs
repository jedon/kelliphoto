using Microsoft.AspNetCore.Mvc;
using KelliPhoto.Web.Models;
using KelliPhoto.Web.Services;

namespace KelliPhoto.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly IContactFormService _contactFormService;

    public ContactController(IContactFormService contactFormService)
    {
        _contactFormService = contactFormService;
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
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var ips = forwardedFor.Split(',');
            return ips[0].Trim();
        }

        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
