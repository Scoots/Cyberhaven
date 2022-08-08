namespace Cyberhaven.Data
{
    internal class JoinManagerMapData
    {
        public JoinRequestQueueMessage Message { get; set; } = default!;
        public Timer Timer { get; set; } = default!;
        public long ReceivedTimeinMilliseconds { get; set; }
    }
}
