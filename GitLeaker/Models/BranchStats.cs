namespace GitLeaker.Models;

public class BranchStats
{
    public string Branch { get; set; } = "";
    public int TotalLeaks { get; set; }
    public int CriticalLeaks { get; set; }
    public double RiskScore { get; set; }
    public List<string> TopLeakers { get; set; } = new();
}