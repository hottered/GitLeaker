using System.Text.RegularExpressions;
using GitLeaker.Models;
using GitLeaker.Services.Interfaces;

namespace GitLeaker.Services;
public class PatternService : IPatternService
{
    public List<SecretPattern> Patterns { get; } = new()
    {
        // Cloud Providers
        new("AWS Access Key", @"(?i)(aws_access_key_id|aws_key)[^\w]*(=|:)\s*['""]?(AKIA[0-9A-Z]{16})['""]?",
            RiskLevel.Critical, "Rotate this AWS key immediately at aws.amazon.com/iam and remove from git history using git-filter-repo."),
 
        new("AWS Secret Key", @"(?i)(aws_secret_access_key|aws_secret)[^\w]*(=|:)\s*['""]?([A-Za-z0-9/+=]{40})['""]?",
            RiskLevel.Critical, "Rotate this AWS secret immediately and audit CloudTrail logs for unauthorized usage.", true, 4.0),
 
        new("Google API Key", @"AIza[0-9A-Za-z\-_]{35}",
            RiskLevel.High, "Revoke this key in Google Cloud Console > APIs & Services > Credentials."),
 
        new("Google OAuth Client Secret", @"(?i)client.secret[^\w]*(=|:)\s*['""]?([a-zA-Z0-9\-_]{24})['""]?",
            RiskLevel.High, "Regenerate OAuth credentials in Google Cloud Console.", true, 3.5),
 
        new("Azure Storage Key", @"(?i)(DefaultEndpointsProtocol|AccountKey)[^\w]*(=|:)\s*['""]?([A-Za-z0-9+/=]{86,88}==)['""]?",
            RiskLevel.Critical, "Regenerate storage account keys in Azure Portal immediately."),
 
        // Source Code Platforms
        new("GitHub Token", @"ghp_[a-zA-Z0-9]{36}",
            RiskLevel.Critical, "Revoke at github.com/settings/tokens. Audit org access logs."),
 
        new("GitHub OAuth Token", @"gho_[a-zA-Z0-9]{36}",
            RiskLevel.High, "Revoke OAuth token in GitHub settings and re-authorize the application."),
 
        new("GitHub App Token", @"(ghu|ghs)_[a-zA-Z0-9]{36}",
            RiskLevel.High, "Rotate GitHub App installation token."),
 
        new("GitLab Token", @"glpat-[a-zA-Z0-9\-]{20}",
            RiskLevel.Critical, "Revoke at gitlab.com/-/user_settings/personal_access_tokens."),
 
        // CI/CD & Dev Tools
        new("NPM Token", @"npm_[a-zA-Z0-9]{36}",
            RiskLevel.High, "Revoke at npmjs.com/settings/<user>/tokens."),
 
        new("Slack Token", @"xox[baprs]-([0-9a-zA-Z]{10,48})",
            RiskLevel.High, "Revoke at api.slack.com/apps. Audit Slack audit logs."),
 
        new("Slack Webhook", @"https://hooks\.slack\.com/services/[A-Z0-9]+/[A-Z0-9]+/[a-zA-Z0-9]+",
            RiskLevel.Medium, "Regenerate the webhook URL in Slack App settings."),
 
        new("Discord Token", @"(?i)(discord[^\w]*(token|secret)[^\w]*(=|:)\s*['""]?[MNO][a-zA-Z0-9._-]{23,})['""]?",
            RiskLevel.High, "Regenerate bot token at discord.com/developers/applications."),
 
        new("Stripe Secret Key", @"sk_(live|test)_[a-zA-Z0-9]{24,}",
            RiskLevel.Critical, "Rotate immediately at dashboard.stripe.com/apikeys. Audit payment logs."),
 
        new("Stripe Publishable Key", @"pk_(live|test)_[a-zA-Z0-9]{24,}",
            RiskLevel.Low, "Publishable keys are less sensitive but should still be managed carefully."),
 
        new("Twilio Auth Token", @"(?i)(twilio[^\w]*(auth_token|authtoken)[^\w]*(=|:)\s*['""]?[a-f0-9]{32})['""]?",
            RiskLevel.High, "Rotate at twilio.com/console. Review SMS/call logs for abuse."),
 
        // Databases
        new("MongoDB Connection String", @"mongodb(\+srv)?://[^\s""'<>]+:[^\s""'<>@]+@[^\s""'<>]+",
            RiskLevel.Critical, "Rotate DB credentials and review MongoDB Atlas access logs."),
 
        new("PostgreSQL Connection String", @"postgres(ql)?://[^\s""'<>]+:[^\s""'<>@]+@[^\s""'<>]+",
            RiskLevel.Critical, "Rotate database password and restrict IP access in pg_hba.conf."),
 
        new("MySQL Connection String", @"mysql://[^\s""'<>]+:[^\s""'<>@]+@[^\s""'<>]+",
            RiskLevel.Critical, "Rotate MySQL credentials and review audit_log tables."),
 
        // Cryptographic & Auth
        new("RSA Private Key", @"-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----",
            RiskLevel.Critical, "This private key must be rotated immediately. Generate a new keypair and revoke the exposed one."),
 
        new("JWT Secret", @"(?i)(jwt[_\-]?(secret|key|token))[^\w]*(=|:)\s*['""]?[a-zA-Z0-9_\-]{32,}['""]?",
            RiskLevel.High, "Rotate the JWT signing secret. All existing tokens will be invalidated.", true, 3.5),
 
        new("Generic API Key", @"(?i)(api[_\-]?(key|token|secret))[^\w]*(=|:)\s*['""]?[a-zA-Z0-9_\-]{20,}['""]?",
            RiskLevel.Medium, "Identify the service this key belongs to and rotate it.", true, 3.5),
 
        new("Generic Password", @"(?i)(password|passwd|pwd|pass)[^\w]*(=|:)\s*['""]?(?!['""]{0,1}\s*$)[^\s""']{8,}['""]?",
            RiskLevel.Medium, "Change this password immediately and use a secrets manager like HashiCorp Vault or AWS Secrets Manager."),
 
        new("Generic Secret", @"(?i)(secret|private[_\-]?key)[^\w]*(=|:)\s*['""]?[^\s""']{16,}['""]?",
            RiskLevel.Medium, "Identify this secret and rotate it. Use environment variables instead of hardcoding.", true, 3.2),
 
        // Email & Communication
        new("SendGrid API Key", @"SG\.[a-zA-Z0-9\-_]{22}\.[a-zA-Z0-9\-_]{43}",
            RiskLevel.High, "Revoke at app.sendgrid.com/settings/api_keys."),
 
        new("Mailgun API Key", @"key-[a-zA-Z0-9]{32}",
            RiskLevel.High, "Rotate at app.mailgun.com/app/account/security/api_keys."),
    };

    public List<(SecretPattern pattern, Match match)> Scan(string line)
    {
        var results = new List<(SecretPattern, Match)>();
 
        foreach (var pattern in Patterns)
        {
            var match = Regex.Match(line, pattern.Regex, RegexOptions.IgnoreCase);
            if (match.Success)
                results.Add((pattern, match));
        }
 
        return results;
    }
 
    public RiskLevel GetRiskFromEntropy(double entropy)
    {
        return entropy switch
        {
            >= 5.0 => RiskLevel.Critical,
            >= 4.0 => RiskLevel.High,
            >= 3.5 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }
}