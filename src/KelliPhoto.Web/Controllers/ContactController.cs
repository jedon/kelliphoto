using Microsoft.AspNetCore.Mvc;
using KelliPhoto.Web.Models;
using KelliPhoto.Web.Services;
using Serilog;

namespace KelliPhoto.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IRateLimitService _rateLimitService;
    private readonly Serilog.ILogger _logger;

    public ContactController(IEmailService emailService, IRateLimitService rateLimitService)
    {
        _emailService = emailService;
        _rateLimitService = rateLimitService;
        _logger = Serilog.Log.ForContext<ContactController>();
    }

    [HttpPost]
    public async Task<IActionResult> SubmitContactForm([FromBody] ContactFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, errors = ModelState });
        }

        // Get client IP address
        var ipAddress = GetClientIpAddress();

        // Check honeypot field - if filled, it's a bot
        if (!string.IsNullOrWhiteSpace(model.Website))
        {
            _logger.Warning("Contact form rejected: honeypot field filled (IP: {IpAddress}, Email: {Email})", 
                ipAddress, model.Email);
            // Return success to bot to avoid revealing our spam protection
            return Ok(new { success = true, message = "Thank you for your message! We'll get back to you soon." });
        }

        // Check rate limiting
        if (_rateLimitService.IsRateLimited(ipAddress))
        {
            _logger.Warning("Contact form rejected: rate limit exceeded (IP: {IpAddress}, Email: {Email})", 
                ipAddress, model.Email);
            return StatusCode(429, new { success = false, message = "Too many submissions. Please try again later." });
        }

        try
        {
            var success = await _emailService.SendContactFormAsync(
                model.Name,
                model.Email,
                model.Subject,
                model.Message);

            if (success)
            {
                // Record successful submission for rate limiting
                _rateLimitService.RecordSubmission(ipAddress);
                
                _logger.Information("Contact form submitted successfully from {Email} (IP: {IpAddress})", 
                    model.Email, ipAddress);
                return Ok(new { success = true, message = "Thank you for your message! We'll get back to you soon." });
            }
            else
            {
                _logger.Warning("Failed to send contact form email from {Email} (IP: {IpAddress})", 
                    model.Email, ipAddress);
                return StatusCode(500, new { success = false, message = "Sorry, there was an error sending your message. Please try again later." });
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing contact form from {Email} (IP: {IpAddress})", 
                model.Email, ipAddress);
            return StatusCode(500, new { success = false, message = "An unexpected error occurred. Please try again later." });
        }
    }

    private string GetClientIpAddress()
    {
        // Try to get the real IP address from headers (for proxied requests)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first one
            var ips = forwardedFor.Split(',');
            return ips[0].Trim();
        }

        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp;
        }

        // Fallback to connection remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
