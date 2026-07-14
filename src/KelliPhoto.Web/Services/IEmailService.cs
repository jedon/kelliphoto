namespace KelliPhoto.Web.Services;

public interface IEmailService
{
    Task<bool> SendEmailAsync(
        string to,
        string subject,
        string body,
        string? fromName = null,
        string? fromEmail = null,
        string? replyToName = null,
        string? replyToEmail = null);
    Task<bool> SendContactFormAsync(string name, string email, string subject, string message);
}
