using GitLeaker.Enums;

namespace GitLeaker.Models;

public record SecretPattern(
    string Name,
    string Regex,
    RiskLevel Risk,
    string Remediation,
    bool RequireEntropy = false,
    double MinEntropy = 3.0
);
