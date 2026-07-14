using KelliPhoto.Web.Models;
using Serilog;

namespace KelliPhoto.Web.Services;

public class ContactFormService : IContactFormService
{
    private readonly IEmailService _emailService;
    private readonly IRateLimitService _rateLimitService;
    private readonly Serilog.ILogger _logger;

    public ContactFormService(IEmailService emailService, IRateLimitService rateLimitService)
    {
        _emailService = emailService;
        _rateLimitService = rateLimitService;
        _logger = Serilog.Log.ForContext<ContactFormService>();
    }

    public async Task<ContactFormSubmitResult> SubmitAsync(ContactFormModel model, string ipAddress)
    {
        // Honeypot: pretend success so bots don't learn about the trap
        if (!string.IsNullOrWhiteSpace(model.Website))
        {
            _logger.Warning(
                "Contact form rejected: honeypot field filled (IP: {IpAddress}, Email: {Email})",
                ipAddress,
                model.Email);
            return new ContactFormSubmitResult(true, "Thank you for your message! We'll get back to you soon.");
        }

        if (_rateLimitService.IsRateLimited(ipAddress))
        {
            _logger.Warning(
                "Contact form rejected: rate limit exceeded (IP: {IpAddress}, Email: {Email})",
                ipAddress,
                model.Email);
            return new ContactFormSubmitResult(false, "Too many submissions. Please try again later.");
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
                _rateLimitService.RecordSubmission(ipAddress);
                _logger.Information(
                    "Contact form submitted successfully from {Email} (IP: {IpAddress})",
                    model.Email,
                    ipAddress);
                return new ContactFormSubmitResult(true, "Thank you for your message! We'll get back to you soon.");
            }

            _logger.Warning(
                "Failed to send contact form email from {Email} (IP: {IpAddress})",
                model.Email,
                ipAddress);
            return new ContactFormSubmitResult(
                false,
                "Sorry, there was an error sending your message. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing contact form from {Email} (IP: {IpAddress})", model.Email, ipAddress);
            return new ContactFormSubmitResult(false, "An unexpected error occurred. Please try again later.");
        }
    }
}
