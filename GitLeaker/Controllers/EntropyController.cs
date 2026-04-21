using GitLeaker.Models;
using GitLeaker.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GitLeaker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EntropyController : ControllerBase
{
    private readonly IEntropyService _entropyService;
 
    public EntropyController(IEntropyService entropyService)
    {
        _entropyService = entropyService;
    }
 
    /// <summary>Check Shannon Entropy of any string</summary>
    [HttpPost("check")]
    public IActionResult CheckEntropy([FromBody] EntropyCheckRequest request)
    {
        if (string.IsNullOrEmpty(request.Input))
            return BadRequest(new { error = "Input is required." });
 
        var entropy = _entropyService.Calculate(request.Input);
        var (token, tokenEntropy) = _entropyService.ExtractHighEntropyToken(request.Input);
 
        return Ok(new
        {
            entropy,
            isLikelySecret = entropy >= 3.5,
            riskLevel = entropy switch
            {
                >= 5.0 => "Critical",
                >= 4.0 => "High",
                >= 3.5 => "Medium",
                >= 2.5 => "Low",
                _ => "Safe"
            },
            highEntropyToken = token,
            tokenEntropy
        });
    }
}