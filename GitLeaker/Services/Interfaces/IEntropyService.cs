namespace GitLeaker.Services.Interfaces;

public interface IEntropyService
{
    double Calculate(string input);

    (string token, double entropy) ExtractHighEntropyToken(string line, double threshold = 3.0);

    string RedactSecret(string line, string secret);
}