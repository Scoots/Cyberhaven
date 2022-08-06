namespace Cyberhaven.Data
{
    public class JoinResponse
    {
        public string Position { get; set; } = default!;
        public string Guid { get; set; } = default!;
        public string? Error { get; set; }
    }
}
