namespace KelliPhoto.Web.Services;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string to, string subject, string body, string? fromName = null, string? fromEmail = null);
    Task<bool> SendContactFormAsync(string name, string email, string subject, string message);
}
