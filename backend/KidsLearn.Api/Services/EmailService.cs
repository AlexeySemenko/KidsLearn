using System.Net;
using System.Net.Mail;

public interface IEmailService
{
    Task<bool> SendInvitationAsync(string toEmail, string? displayName, string inviterName);
    Task<bool> SendParentLinkedAsync(string toEmail, string? displayName, string linkedByEmail);
    Task<bool> SendFriendInviteAsync(string toEmail, string inviterName, string inviteUrl);
    Task<bool> SendAssignmentCompletedToParentAsync(string toEmail, string parentName, string childName, string lessonTitle, decimal score, int correctAnswers, int totalQuestions, IList<(string LessonTitle, decimal Score)> recentResults);
    Task<bool> SendAssignmentCreatedToChildAsync(string toEmail, string childName, string lessonTitle, string subject, DateTime? dueDate);
    Task<bool> SendWelcomeToParentAsync(string toEmail, string? displayName);
    Task<bool> SendChildAddedToParentAsync(string toEmail, string? parentName, string childName, int grade);
    Task<bool> SendChildWelcomeAsync(string toEmail, string childName, string parentEmail, string registerUrl);
    Task<bool> SendChildRegisteredToParentAsync(string toEmail, string parentName, string childName);
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
                <a href="{config["FrontendBaseUrl"]?.TrimEnd('/') ?? "https://kidslearn.fly.dev"}" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
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
                <a href="{config["FrontendBaseUrl"]?.TrimEnd('/') ?? "https://kidslearn.fly.dev"}" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
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

    public async Task<bool> SendFriendInviteAsync(string toEmail, string inviterName, string inviteUrl)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "Email not configured — friend invite would be sent to {Email} from {Inviter}, url: {Url}",
                toEmail, inviterName, inviteUrl);
            return false;
        }

        var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var username = config["Email:SmtpUsername"] ?? string.Empty;
        var password = config["Email:SmtpPassword"] ?? string.Empty;
        var fromAddress = config["Email:FromAddress"] ?? username;
        var fromName = config["Email:FromName"] ?? "KidsLearnAI";

        var body = $"""
            <html><body style="font-family:sans-serif;color:#0f2745;max-width:520px;margin:0 auto;">
              <h2 style="color:#0f2745;">Friend request on KidsLearnAI 🤝</h2>
              <p>Hi!</p>
              <p><strong>{inviterName}</strong> wants to be friends with you on <strong>KidsLearnAI</strong>!</p>
              <p style="margin-top:1.5rem;">
                <a href="{inviteUrl}" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
                  Accept friend request
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
                Subject = $"{inviterName} wants to be your friend on KidsLearnAI",
                Body = body,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toEmail));

            await client.SendMailAsync(message);
            logger.LogInformation("Friend invite sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send friend invite to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendAssignmentCompletedToParentAsync(string toEmail, string parentName, string childName, string lessonTitle, decimal score, int correctAnswers, int totalQuestions, IList<(string LessonTitle, decimal Score)> recentResults)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "Email not configured — assignment completion would be sent to parent {Email} for child {Child}, lesson {Lesson}, score {Score}",
                toEmail, childName, lessonTitle, score);
            return false;
        }

        var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var username = config["Email:SmtpUsername"] ?? string.Empty;
        var password = config["Email:SmtpPassword"] ?? string.Empty;
        var fromAddress = config["Email:FromAddress"] ?? username;
        var fromName = config["Email:FromName"] ?? "KidsLearnAI";

        var scoreColor = score >= 80 ? "#10b981" : score >= 50 ? "#f59e0b" : "#ef4444";
        var emoji = score >= 80 ? "🌟" : score >= 50 ? "👍" : "💪";

        var recentRowsHtml = recentResults.Count > 0
            ? string.Join("", recentResults.Select(r =>
            {
                var c = r.Score >= 80 ? "#10b981" : r.Score >= 50 ? "#f59e0b" : "#ef4444";
                return $"<tr><td style=\"padding:6px 8px;border-bottom:1px solid #e5e7eb;\">{System.Net.WebUtility.HtmlEncode(r.LessonTitle)}</td><td style=\"padding:6px 8px;border-bottom:1px solid #e5e7eb;text-align:center;font-weight:700;color:{c};\">{r.Score}%</td></tr>";
            }))
            : "<tr><td colspan=\"2\" style=\"padding:6px 8px;color:#888;\">No previous results yet.</td></tr>";

        var body = $"""
            <html><body style="font-family:sans-serif;color:#0f2745;max-width:520px;margin:0 auto;">
              <h2 style="color:#0f2745;">{emoji} {childName} completed a lesson!</h2>
              <p>Hi {System.Net.WebUtility.HtmlEncode(parentName)},</p>
              <p><strong>{System.Net.WebUtility.HtmlEncode(childName)}</strong> just finished <strong>{System.Net.WebUtility.HtmlEncode(lessonTitle)}</strong>.</p>
              <div style="background:#f8fafc;border-radius:10px;padding:16px 20px;margin:20px 0;text-align:center;">
                <div style="font-size:2.5rem;font-weight:800;color:{scoreColor};">{score}%</div>
                <div style="color:#64748b;font-size:0.95rem;">{correctAnswers} out of {totalQuestions} correct</div>
              </div>
              {(recentResults.Count > 0 ? $"""
              <h3 style="color:#0f2745;margin-top:1.5rem;">Recent results</h3>
              <table style="width:100%;border-collapse:collapse;font-size:0.9rem;">
                <thead><tr style="background:#f1f5f9;"><th style="padding:6px 8px;text-align:left;">Lesson</th><th style="padding:6px 8px;">Score</th></tr></thead>
                <tbody>{recentRowsHtml}</tbody>
              </table>
              """ : "")}
              <p style="margin-top:1.5rem;">
                <a href="{config["FrontendBaseUrl"]?.TrimEnd('/') ?? "https://kidslearn.fly.dev"}" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
                  View full report
                </a>
              </p>
              <p style="color:#888;font-size:0.85rem;margin-top:2rem;">You're receiving this because you're a parent on KidsLearnAI.</p>
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
                Subject = $"{childName} completed \"{lessonTitle}\" — {score}% {emoji}",
                Body = body,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toEmail, parentName));

            await client.SendMailAsync(message);
            logger.LogInformation("Assignment completion email sent to parent {Email} for child {Child}", toEmail, childName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send assignment completion email to parent {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendAssignmentCreatedToChildAsync(string toEmail, string childName, string lessonTitle, string subject, DateTime? dueDate)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "Email not configured — assignment notification would be sent to child {Email}, lesson {Lesson}",
                toEmail, lessonTitle);
            return false;
        }

        var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var username = config["Email:SmtpUsername"] ?? string.Empty;
        var password = config["Email:SmtpPassword"] ?? string.Empty;
        var fromAddress = config["Email:FromAddress"] ?? username;
        var fromName = config["Email:FromName"] ?? "KidsLearnAI";

        var dueDateHtml = dueDate.HasValue
            ? $"<p>📅 <strong>Due date:</strong> {dueDate.Value.ToLocalTime():MMMM d, yyyy}</p>"
            : "";

        var body = $"""
            <html><body style="font-family:sans-serif;color:#0f2745;max-width:520px;margin:0 auto;">
              <h2 style="color:#0f2745;">📚 You have a new lesson!</h2>
              <p>Hi {System.Net.WebUtility.HtmlEncode(childName)},</p>
              <p>Your parent has assigned you a new lesson on <strong>KidsLearnAI</strong>:</p>
              <div style="background:#f8fafc;border-radius:10px;padding:16px 20px;margin:20px 0;">
                <div style="font-size:1.2rem;font-weight:700;color:#0f2745;">{System.Net.WebUtility.HtmlEncode(lessonTitle)}</div>
                <div style="color:#64748b;font-size:0.9rem;margin-top:4px;">Subject: {System.Net.WebUtility.HtmlEncode(subject)}</div>
              </div>
              {dueDateHtml}
              <p style="margin-top:1.5rem;">
                <a href="{config["FrontendBaseUrl"]?.TrimEnd('/') ?? "https://kidslearn.fly.dev"}" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
                  Start lesson 🚀
                </a>
              </p>
              <p style="color:#888;font-size:0.85rem;margin-top:2rem;">You're receiving this because your parent assigned a lesson to you on KidsLearnAI.</p>
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
                Subject = $"New lesson assigned: {lessonTitle} 📚",
                Body = body,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toEmail, childName));

            await client.SendMailAsync(message);
            logger.LogInformation("Assignment notification sent to child {Email}, lesson {Lesson}", toEmail, lessonTitle);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send assignment notification to child {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendWelcomeToParentAsync(string toEmail, string? displayName)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("Email not configured — welcome email would be sent to {Email}", toEmail);
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
              <h2 style="color:#0f2745;">Welcome to KidsLearnAI 🚀</h2>
              <p>Hi {System.Net.WebUtility.HtmlEncode(recipientName)},</p>
              <p>Your parent account has been created. You can now add children, create lessons, assign work, and track progress — all in one place.</p>
              <p style="margin-top:1.5rem;">
                <a href="{config["FrontendBaseUrl"]?.TrimEnd('/') ?? "https://kidslearn.fly.dev"}" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
                  Go to dashboard
                </a>
              </p>
              <p style="color:#888;font-size:0.85rem;margin-top:2rem;">You're receiving this because you registered on KidsLearnAI.</p>
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
                Subject = "Welcome to KidsLearnAI 🚀",
                Body = body,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toEmail, recipientName));
            await client.SendMailAsync(message);
            logger.LogInformation("Welcome email sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send welcome email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendChildAddedToParentAsync(string toEmail, string? parentName, string childName, int grade)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("Email not configured — child-added notification would be sent to parent {Email} for child {Child}", toEmail, childName);
            return false;
        }

        var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var username = config["Email:SmtpUsername"] ?? string.Empty;
        var password = config["Email:SmtpPassword"] ?? string.Empty;
        var fromAddress = config["Email:FromAddress"] ?? username;
        var fromName = config["Email:FromName"] ?? "KidsLearnAI";
        var recipientName = string.IsNullOrWhiteSpace(parentName) ? toEmail : parentName;

        var body = $"""
            <html><body style="font-family:sans-serif;color:#0f2745;max-width:520px;margin:0 auto;">
              <h2 style="color:#0f2745;">A new child has been added 🎉</h2>
              <p>Hi {System.Net.WebUtility.HtmlEncode(recipientName)},</p>
              <p>A child profile has been created in your KidsLearnAI account:</p>
              <div style="background:#f8fafc;border-radius:10px;padding:16px 20px;margin:20px 0;">
                <div style="font-size:1.1rem;font-weight:700;color:#0f2745;">{System.Net.WebUtility.HtmlEncode(childName)}</div>
                <div style="color:#64748b;font-size:0.9rem;margin-top:4px;">Grade {grade}</div>
              </div>
              <p>You can now assign lessons and track their progress from your dashboard.</p>
              <p style="margin-top:1.5rem;">
                <a href="{config["FrontendBaseUrl"]?.TrimEnd('/') ?? "https://kidslearn.fly.dev"}" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
                  Open dashboard
                </a>
              </p>
              <p style="color:#888;font-size:0.85rem;margin-top:2rem;">You're receiving this because you're a parent on KidsLearnAI.</p>
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
                Subject = $"{childName} has been added to your KidsLearnAI account",
                Body = body,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toEmail, recipientName));
            await client.SendMailAsync(message);
            logger.LogInformation("Child-added notification sent to parent {Email} for child {Child}", toEmail, childName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send child-added notification to parent {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendChildWelcomeAsync(string toEmail, string childName, string parentEmail, string registerUrl)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("Email not configured — child welcome email would be sent to {Email} with register link {Url}", toEmail, registerUrl);
            return false;
        }

        var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var username = config["Email:SmtpUsername"] ?? string.Empty;
        var password = config["Email:SmtpPassword"] ?? string.Empty;
        var fromAddress = config["Email:FromAddress"] ?? username;
        var fromName = config["Email:FromName"] ?? "KidsLearnAI";
        var safeRegisterUrl = System.Net.WebUtility.HtmlEncode(registerUrl);

        var body = $"""
            <html><body style="font-family:sans-serif;color:#0f2745;max-width:520px;margin:0 auto;">
              <h2 style="color:#0f2745;">You've been added to KidsLearnAI 🎓</h2>
              <p>Hi {System.Net.WebUtility.HtmlEncode(childName)},</p>
              <p>Your parent ({System.Net.WebUtility.HtmlEncode(parentEmail)}) has enrolled you in <strong>KidsLearnAI</strong> — a learning platform just for you!</p>
              <p>Click the button below to create your password and start learning:</p>
              <p style="margin-top:1.5rem;">
                <a href="{safeRegisterUrl}" style="background:#f4d35e;color:#0f2745;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;">
                  Complete registration 🚀
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
                Subject = "You've been added to KidsLearnAI 🎓",
                Body = body,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toEmail, childName));
            await client.SendMailAsync(message);
            logger.LogInformation("Child welcome email sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send child welcome email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendChildRegisteredToParentAsync(string toEmail, string parentName, string childName)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("Email not configured — child registered notification would be sent to {Email} for child {Child}", toEmail, childName);
            return false;
        }

        var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var username = config["Email:SmtpUsername"] ?? string.Empty;
        var password = config["Email:SmtpPassword"] ?? string.Empty;
        var fromAddress = config["Email:FromAddress"] ?? username;
        var fromName = config["Email:FromName"] ?? "KidsLearnAI";

        var body = $"""
            <html><body style="font-family:sans-serif;color:#0f2745;max-width:520px;margin:0 auto;">
              <h2 style="color:#0f2745;">{System.Net.WebUtility.HtmlEncode(childName)} has joined KidsLearnAI 🎉</h2>
              <p>Hi {System.Net.WebUtility.HtmlEncode(parentName)},</p>
              <p><strong>{System.Net.WebUtility.HtmlEncode(childName)}</strong> just completed their account setup and is ready to start learning!</p>
              <p>You can now assign lessons and track their progress from your parent dashboard.</p>
              <p style="color:#888;font-size:0.85rem;margin-top:2rem;">This is an automated notification from KidsLearnAI.</p>
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
                Subject = $"{childName} has joined KidsLearnAI 🎉",
                Body = body,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toEmail, parentName));
            await client.SendMailAsync(message);
            logger.LogInformation("Child registered notification sent to parent {Email} for child {Child}", toEmail, childName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send child registered notification to {Email}", toEmail);
            return false;
        }
    }
}
