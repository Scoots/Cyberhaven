using Cyberhaven.Data;

namespace Cyberhaven
{
    internal class ClientJoinManager
    {
        // Lazy singleton setup, so everything will interact with this instance
        private static readonly Lazy<ClientJoinManager> lazy = new(() => new ClientJoinManager());
        public static ClientJoinManager Instance { get { return lazy.Value; } }

        // The map of our statistics, along with the highest key we have and its associated value
        public readonly Dictionary<string, long> StatsMap = new();
        private string _highGuid = string.Empty;
        private long _highGuidValue = long.MinValue;

        // Private queue that all enqueue requests will go into
        // Made it private so we could lock on it safely
        private readonly Queue<ApiQueueMessage> _requestQueue = new();

        // Map of data that is transitioned from the API into this manager
        // It will track all user data, including their timer subscriptions and task information
        private readonly Dictionary<int, ClientJoinManagerMapData> _userMap = new();

        // The id of the first user, or null if we don't have one
        private int? _firstUserId = null;

        private ClientJoinManager()
        {
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
                                // If user already exists in map, close their old connection and timer
                                if (_userMap.ContainsKey(message.UserId))
                                {
                                    RemoveFromMap(message.UserId);
                                }
                            }

                            var timer = new Timer(callback, null, 10000, Timeout.Infinite);
                            void callback(object? _)
                            {
                                // Lock on the userMap to safely interact with it
                                lock (_userMap)
                                {
                                    // Verify the entry is still there - if it isn't, then leave
                                    if (!_userMap.ContainsKey(message.UserId))
                                    {
                                        return;
                                    }

                                    var mapMessage = _userMap[message.UserId];
                                    var taskCompletionSource = mapMessage.Message.TaskSource;

                                    // Set the result on the task source taht was passed in from the API
                                    taskCompletionSource.SetResult(new JoinResponse() { Guid = string.Empty, Position = string.Empty, Error = "Timeout" });
                                    RemoveFromMap(message.UserId);
                                    _firstUserId = null;
                                }
                            }

                            // Lock on the userMap to safely interact with it
                            lock (_userMap)
                            {
                                // Storing the timer object for cleanup and the milliseconds for Stats call
                                var mapMessage = new ClientJoinManagerMapData()
                                {
                                    Message = message,
                                    Timer = timer,
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

        public void Enqueue(ApiQueueMessage item)
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
            lock (StatsMap)
            {
                if (StatsMap.ContainsKey(guid))
                {
                    throw new Exception("This GUID has already been added, which should never happen");
                }

                // Find our millisecond difference (not micro in C#, sorry)
                var millisecondsToJoin = millisecondEnd - millisecondStart;

                // Always add to our map - this is accounted for with > in our count check below
                StatsMap[guid] = millisecondsToJoin;

                // Update our high value, this is O(1) but is unnecessary if we are over 50000
                // I'm not looking for these optimizations right now
                if (millisecondsToJoin > _highGuidValue)
                {
                    _highGuid = guid;
                    _highGuidValue = millisecondsToJoin;
                }

                // If we are over our desired count, remove the highest and find the new highest
                if (StatsMap.Count > 50000)
                {
                    // After we remove the highest value, we need to find the new highest
                    // We can do that by doing an O(N) operation over the dictionary, and pulling
                    // the new high value is O(1) because this is a hash map
                    StatsMap.Remove(_highGuid); // O(1)
                    _highGuid = StatsMap.MaxBy(kvp => kvp.Value).Key; // O(N)
                    _highGuidValue = StatsMap[_highGuid]; // O(1)
                }
            }
        }
    }
}
