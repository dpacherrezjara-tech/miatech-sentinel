namespace CredentialScanner.Models
{
    public class CredentialRule
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string Pattern { get; set; }
        public string Severity { get; set; }
        public string FilterType { get; set; }
    }
}