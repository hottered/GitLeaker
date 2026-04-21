using GitLeaker.Services.Interfaces;

namespace GitLeaker.Services;

public class EntropyService : IEntropyService
{
    // Shannon Entropy: H = -sum(p(x) * log2(p(x)))
    public double Calculate(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;
 
        var frequency = new Dictionary<char, int>();
        foreach (var c in input)
            frequency[c] = frequency.GetValueOrDefault(c, 0) + 1;
 
        double entropy = 0;
        int length = input.Length;
 
        foreach (var count in frequency.Values)
        {
            double probability = (double)count / length;
            entropy -= probability * Math.Log2(probability);
        }
 
        return Math.Round(entropy, 4);
    }
 
    // Extract the highest-entropy token from a line (the likely secret value)
    public (string token, double entropy) ExtractHighEntropyToken(string line, double threshold = 3.0)
    {
        // Split on common delimiters: =, :, ", ', space
        var parts = line.Split(new[] { '=', ':', '"', '\'', ' ', '\t', ',', ';' },
            StringSplitOptions.RemoveEmptyEntries);
 
        string bestToken = "";
        double bestEntropy = 0;
 
        foreach (var part in parts)
        {
            // Only consider tokens of meaningful length (secrets are usually 16+ chars)
            if (part.Length < 8) continue;
 
            var e = Calculate(part);
            if (e > bestEntropy && e >= threshold)
            {
                bestEntropy = e;
                bestToken = part;
            }
        }
 
        return (bestToken, bestEntropy);
    }
 
    // Redact a secret from a line for safe display
    public string RedactSecret(string line, string secret)
    {
        if (string.IsNullOrEmpty(secret)) return line;
        int visibleChars = Math.Min(4, secret.Length / 4);
        string redacted = secret[..visibleChars] + new string('*', secret.Length - visibleChars);
        return line.Replace(secret, redacted);
    }
}