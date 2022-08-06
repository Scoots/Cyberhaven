namespace Cyberhaven.Data
{
    internal class ClientJoinManagerMapData
    {
        public ApiQueueMessage Message { get; set; } = default!;
        public Timer Timer { get; set; } = default!;
        public long ReceivedTimeinMilliseconds { get; set; }
    }
}
