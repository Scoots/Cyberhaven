namespace Cyberhaven.Data
{
    internal class ApiQueueMessage
    {
        public int UserId { get; set; }
        public TaskCompletionSource<JoinResponse> TaskSource { get; set; } = default!;
    }
}
