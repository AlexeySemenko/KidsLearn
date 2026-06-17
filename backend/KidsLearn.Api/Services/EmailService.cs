using System.Net;
using System.Net.Mail;

public interface IEmailService
{
    Task<bool> SendInvitationAsync(string toEmail, string? displayName, string inviterName);
    Task<bool> SendParentLinkedAsync(string toEmail, string? displayName, string linkedByEmail);
}

public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    public async Task<bool> SendInvitationAsync(string toEmail, string? displayName, string inviterName)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "Email not configured — invitation would be sent to {Email} (display name: {Name}) by {Inviter}",
                toEmail, displayName, inviterName);
            return false;
        }

        var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var username = config["Email:SmtpUsername"] ?? string.Empty;
        var password = config["Email:SmtpPassword"] ?? string.Empty;
        var fromAddress = config["Email:FromAddress"] ?? username;
        var fromName = config["Email:FromName"] ?? "KidsLearnAI";

        var recipientName = string.IsNullOrWhiteSpace(displayName) ? toEmail : displayName;

        var body = $"""
            <html><body style="font-family:sans-serif;color:#0f2745;max-width:520px;margin:0 auto;">
              <h2 style="color:#0f2745;">You've been invited to KidsLearnAI 🚀</h2>
              <p>Hi {recipientName},</p>
              <p><strong>{inviterName}</strong> has invited you to join <strong>KidsLearnAI</strong> — the smart learning platform for kids and parents.</p>
              <p style="margin-top:1.5rem;">
                <a href="https://kidslearn.fly.dev/" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
                  Sign in with Google
                </a>
              </p>
              <p style="color:#888;font-size:0.85rem;margin-top:2rem;">If you weren't expecting this, you can safely ignore this email.</p>
            </body></html>
            """;

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = $"You're invited to KidsLearnAI",
                Body = body,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toEmail, recipientName));

            await client.SendMailAsync(message);
            logger.LogInformation("Invitation sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send invitation email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendParentLinkedAsync(string toEmail, string? displayName, string linkedByEmail)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "Email not configured — parent link notification would be sent to {Email} (linked by {LinkedBy})",
                toEmail, linkedByEmail);
            return false;
        }

        var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var username = config["Email:SmtpUsername"] ?? string.Empty;
        var password = config["Email:SmtpPassword"] ?? string.Empty;
        var fromAddress = config["Email:FromAddress"] ?? username;
        var fromName = config["Email:FromName"] ?? "KidsLearnAI";

        var recipientName = string.IsNullOrWhiteSpace(displayName) ? toEmail : displayName;

        var body = $"""
            <html><body style="font-family:sans-serif;color:#0f2745;max-width:520px;margin:0 auto;">
              <h2 style="color:#0f2745;">Your account has been linked 🔗</h2>
              <p>Hi {recipientName},</p>
              <p><strong>{linkedByEmail}</strong> has linked their KidsLearnAI parent account with yours.</p>
              <p>You now share the same workspace — you will both see and manage the same children, lessons, assignments, and reports together.</p>
              <p style="margin-top:1.5rem;">
                <a href="https://kidslearn.fly.dev/" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
                  Open KidsLearnAI
                </a>
              </p>
              <p style="color:#888;font-size:0.85rem;margin-top:2rem;">If you weren't expecting this, please contact the KidsLearnAI team.</p>
            </body></html>
            """;

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = "Your KidsLearnAI account has been linked",
                Body = body,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toEmail, recipientName));

            await client.SendMailAsync(message);
            logger.LogInformation("Parent link notification sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send parent link notification to {Email}", toEmail);
            return false;
        }
    }
}
