using GitLeaker.Services.Interfaces;

namespace GitLeaker.Services;

public class EntropyService : IEntropyService
{
    public double Calculate(string input)
    {
        if (string.IsNullOrEmpty(input)) 
            throw new ArgumentException("Input must not be null or empty.", nameof(input));
        
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
 
    public (string token, double entropy) ExtractHighEntropyToken(string line, double threshold = 3.0)
    {
        if (string.IsNullOrEmpty(line))
            throw new ArgumentException("Line must not be null or empty.", nameof(line));

        var parts = line.Split(new[] { '=', ':', '"', '\'', ' ', '\t', ',', ';' },
            StringSplitOptions.RemoveEmptyEntries);
 
        string bestToken = "";
        double bestEntropy = 0;
 
        foreach (var part in parts)
        {
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
 
    public string RedactSecret(string line, string secret)
    {
        if (string.IsNullOrEmpty(secret))
            throw new ArgumentException("Secret must not be null or empty.", nameof(secret));
        
        int visibleChars = Math.Min(4, secret.Length / 4);
        string redacted = secret[..visibleChars] + new string('*', secret.Length - visibleChars);
        return line.Replace(secret, redacted);
    }
}