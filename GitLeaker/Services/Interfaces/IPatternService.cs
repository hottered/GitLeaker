using System.Text.RegularExpressions;
using GitLeaker.Models;

namespace GitLeaker.Services.Interfaces;

public interface IPatternService
{
    List<SecretPattern> Patterns { get; }

    List<(SecretPattern pattern, Match match)> Scan(string line);

    RiskLevel GetRiskFromEntropy(double entropy);
}