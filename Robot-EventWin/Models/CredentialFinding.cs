using System;

namespace CredentialScanner.Models
{
    public class CredentialFinding
    {
        public string FilePath { get; set; }
        public string RuleId { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
        public string Secret { get; set; }
        public int LineNumber { get; set; }
        public string FullMatch { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }
}