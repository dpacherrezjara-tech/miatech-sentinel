using System.Collections.Generic;

namespace CredentialScanner.Models
{
    public class RiskScore
    {
        public string Line { get; set; }
        public int LineNumber { get; set; }
        public int Score { get; set; }
        public string Severity { get; set; }
        public List<string> Reasons { get; set; } = new();
        public string RuleId { get; set; }
        public string Description { get; set; }
    }
}