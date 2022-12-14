using Cyberhaven.Data;

namespace Cyberhaven
{
    public interface IJoinManager
    {
        void Enqueue(JoinRequestQueueMessage message);
    }

    internal class JoinManager : IJoinManager
    {
        // Private queue that all enqueue requests will go into
        // Made it private so we could lock on it safely
        private readonly Queue<JoinRequestQueueMessage> _requestQueue = new();
        private readonly IStatsMap _statsMap;

        // Map of data that is transitioned from the API into this manager
        // It will track all user data, including their timer subscriptions and task information
        // We will lock on it frequently, because we can call it from the timer callback or from the
        // processing thread
        private readonly Dictionary<int, JoinManagerMapData> _userMap = new();

        // The id of the first user, or null if we don't have one
        private int? _firstUserId = null;

        public JoinManager(IStatsMap statsMap)
        {
            _statsMap = statsMap;

            // Because we want this to be single threaded, we make sure this can only ever be invoked
            // on the same thread. We will never have two requests coming in at the same time in this factory
            // That is not to say it won't be an issue; we are still adding objects to the queue from the API thread
            // We keep it safe (with race conditions and edge cases) by locking on our queue when we add
            // and pop, and locking on our map whenever we are interacting with it since the timer is *not*
            // guaranteed to be on the same thread.
            Task.Factory.StartNew(() =>
            {
                // Going to be looping on this forever. We're returning a task that we aren't awaiting
                // so this will last until the application shuts down
                while (true)
                {
                    lock (_requestQueue)
                    {
                        while (_requestQueue.Count > 0)
                        {
                            var message = _requestQueue.Dequeue();

                            lock (_userMap)
                            {
                                // If user already exists in map, cleanup their old request
                                if (_userMap.ContainsKey(message.UserId))
                                {
                                    RemoveFromMap(message.UserId);
                                }
                            }

                            // Lock on the userMap to safely interact with it
                            lock (_userMap)
                            {
                                // Storing the timer object for cleanup and the milliseconds for Stats call
                                var mapMessage = new JoinManagerMapData()
                                {
                                    Message = message,
                                    Timer = GetTimer(message.UserId),
                                    ReceivedTimeinMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                                };
                                _userMap.Add(message.UserId, mapMessage);
                            }

                            // If we don't have a first user, save it and keep going
                            if (!_firstUserId.HasValue)
                            {
                                _firstUserId = message.UserId;
                            }
                            else
                            {
                                // Locking to prevent our map from changing underneath us
                                lock (_userMap)
                                {
                                    // If we do have a first user, send messages to first and second
                                    var id = Guid.NewGuid();
                                    var firstClient = _userMap[_firstUserId.Value];
                                    var secondClient = _userMap[message.UserId];

                                    var firstResponse = new JoinResponse
                                    {
                                        Position = "First",
                                        Guid = id.ToString()
                                    };

                                    var secondResponse = new JoinResponse
                                    {
                                        Position = "Second",
                                        Guid = id.ToString()
                                    };

                                    firstClient.Message.TaskSource.SetResult(firstResponse);
                                    secondClient.Message.TaskSource.SetResult(secondResponse);

                                    // Populates our map for the stats call later
                                    AddToStatsMap(id.ToString(), firstClient.ReceivedTimeinMilliseconds, secondClient.ReceivedTimeinMilliseconds);

                                    RemoveFromMap(firstClient.Message.UserId);
                                    RemoveFromMap(secondClient.Message.UserId);
                                    _firstUserId = null;
                                }
                            }
                        }

                        Monitor.Wait(_requestQueue);
                    }
                }
            },
            CancellationToken.None,
            TaskCreationOptions.RunContinuationsAsynchronously,
            TaskScheduler.Current);
        }

        public void Enqueue(JoinRequestQueueMessage item)
        {
            lock (_requestQueue)
            {
                _requestQueue.Enqueue(item);
                Monitor.PulseAll(_requestQueue);
            }
        }

        private void RemoveFromMap(int userId)
        {
            // Still locking on userMap for safety
            // Locking on the same thread is reentrant, so this is fine and extra safe
            lock (_userMap)
            {
                if (!_userMap.ContainsKey(userId))
                {
                    return;
                }

                var message = _userMap[userId];

                message.Timer.Dispose();
                _userMap.Remove(userId);
            }
        }

        private void AddToStatsMap(string guid, long millisecondStart, long millisecondEnd)
        {
            // Don't need to lock here, since this is only called from a single-threaded space
            // However, safer to do it until I can test thoroughly
            lock (_statsMap)
            {
                // Find our millisecond difference (not micro in C#, sorry)
                var millisecondsToJoin = millisecondEnd - millisecondStart;
                _statsMap.Add(guid, millisecondsToJoin);
            }
        }

        private Timer GetTimer(int userId)
        {
            var timer = new Timer(timerCallback, null, 10000, Timeout.Infinite);
            void timerCallback(object? _)
            {
                // Lock on the userMap to safely interact with it
                lock (_userMap)
                {
                    // Verify the entry is still there - if it isn't, then leave
                    if (!_userMap.ContainsKey(userId))
                    {
                        return;
                    }

                    var mapMessage = _userMap[userId];
                    var taskCompletionSource = mapMessage.Message.TaskSource;

                    // Set the result on the task source taht was passed in from the API
                    taskCompletionSource.SetResult(new JoinResponse() { Guid = string.Empty, Position = string.Empty, Error = "Timeout" });
                    RemoveFromMap(userId);
                    _firstUserId = null;
                }
            }

            return timer;
        }
    }
}
