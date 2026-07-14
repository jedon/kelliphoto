using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using Serilog;

namespace KelliPhoto.Web.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly Serilog.ILogger _logger;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
        _logger = Serilog.Log.ForContext<EmailService>();
    }

    public async Task<bool> SendEmailAsync(
        string to,
        string subject,
        string body,
        string? fromName = null,
        string? fromEmail = null,
        string? replyToName = null,
        string? replyToEmail = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost) || string.IsNullOrWhiteSpace(_settings.SmtpUsername))
        {
            _logger.Warning("Email service not configured. SMTP settings are missing.");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            // Always send as the authenticated mailbox; Gmail rejects arbitrary From addresses.
            message.From.Add(new MailboxAddress(
                fromName ?? _settings.FromName ?? "Kelli Thompson Photography",
                fromEmail ?? _settings.FromEmail ?? _settings.SmtpUsername));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;

            if (!string.IsNullOrWhiteSpace(replyToEmail))
            {
                message.ReplyTo.Add(new MailboxAddress(
                    replyToName ?? replyToEmail,
                    replyToEmail));
            }

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body.Replace("\n", "<br>")
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            // Connect to SMTP server
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            
            // Authenticate
            if (!string.IsNullOrWhiteSpace(_settings.SmtpUsername))
            {
                await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword);
            }

            // Send email
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.Information("Email sent successfully to {To} with subject: {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to send email to {To} with subject: {Subject}", to, subject);
            return false;
        }
    }

    public async Task<bool> SendContactFormAsync(string name, string email, string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(_settings.ContactEmail))
        {
            _logger.Warning("Contact email not configured. Cannot send contact form.");
            return false;
        }

        var body = $@"
<h2>New Contact Form Submission</h2>
<p><strong>From:</strong> {System.Net.WebUtility.HtmlEncode(name)} ({System.Net.WebUtility.HtmlEncode(email)})</p>
<p><strong>Subject:</strong> {System.Net.WebUtility.HtmlEncode(subject)}</p>
<hr>
<p><strong>Message:</strong></p>
<p>{System.Net.WebUtility.HtmlEncode(message).Replace("\n", "<br>")}</p>
<hr>
<p><em>This message was sent from the contact form on Kelli Thompson Photography website.</em></p>";

        return await SendEmailAsync(
            to: _settings.ContactEmail,
            subject: $"Contact Form: {subject}",
            body: body,
            replyToName: name,
            replyToEmail: email);
    }
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public string? ContactEmail { get; set; }
}
