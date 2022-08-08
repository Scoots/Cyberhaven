namespace Cyberhaven.Data
{
    /// <summary>
    /// Message sent from the controller to the JoinManager when a new join request is received
    /// </summary>
    public class JoinRequestQueueMessage
    {
        public int UserId { get; set; }
        public TaskCompletionSource<JoinResponse> TaskSource { get; set; } = default!;
    }
}
