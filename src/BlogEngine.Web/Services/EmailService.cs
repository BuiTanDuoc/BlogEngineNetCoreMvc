using System.Net;
using System.Net.Mail;

namespace BlogEngine.Web.Services;

public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public bool EnableSsl { get; set; } = true;
    public string From { get; set; } = "";
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, string? replyTo = null);
}

public class EmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(Microsoft.Extensions.Options.IOptions<SmtpOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, string? replyTo = null)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(to))
        {
            _logger.LogInformation("SMTP chưa được cấu hình (Smtp:Host) - bỏ qua gửi mail '{Subject}'.", subject);
            return;
        }

        try
        {
            var from = string.IsNullOrWhiteSpace(_options.From) ? _options.UserName : _options.From;
            using var message = new MailMessage(from, to, subject, htmlBody) { IsBodyHtml = true };
            if (!string.IsNullOrWhiteSpace(replyTo)) message.ReplyToList.Add(replyTo);

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                Timeout = 10_000
            };
            if (!string.IsNullOrEmpty(_options.UserName))
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password);

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Lỗi gửi mail không được làm hỏng request của người dùng.
            _logger.LogError(ex, "Không gửi được email '{Subject}' tới {To}.", subject, to);
        }
    }
}
