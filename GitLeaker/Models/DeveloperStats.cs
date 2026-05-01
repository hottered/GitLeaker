namespace GitLeaker.Models;

public class DeveloperStats
{
    public string Author { get; set; } = "";
    public string Email { get; set; } = "";
    public int TotalLeaks { get; set; }
    public int CriticalLeaks { get; set; }
    public int HighLeaks { get; set; }
    public int MediumLeaks { get; set; }
    public int LowLeaks { get; set; }
    public double RiskScore { get; set; }
    public string MostLeakedType { get; set; } = "";
    public DateTime? LastLeakDate { get; set; }
    public List<string> Branches { get; set; } = new();
}