namespace Cyberhaven.Data
{
    internal class StatsMap
    {
        private static readonly Lazy<StatsMap> lazy = new(() => new StatsMap());
        public static StatsMap Instance { get { return lazy.Value; } }

        private const int _maximumStatsStored = 50000;
        private string _highKey = string.Empty;
        private long _highValue = long.MinValue;

        private readonly Dictionary<string, long> _map = new();
        public Dictionary<string, long> Stats
        { 
            get
            {
                // Shallow copy should be fine here, just string and long
                return _map.ToDictionary(entry => entry.Key, entry => entry.Value);
            } 
        }

        public bool ContainsKey(string key)
        {
            return _map.ContainsKey(key);
        }

        public void Add(string key, long value)
        {
            if (_map.ContainsKey(key))
            {
                throw new Exception($"This key has already been added, which should never happen: {key}");
            }

            _map[key] = value;

            // Update our high value, this is O(1) but is unnecessary if we are over 50000
            // I'm not looking for these optimizations right now
            if (value > _highValue)
            {
                _highKey = key;
                _highValue = value;
            }

            // If we are over our desired count, remove the highest and find the new highest
            if (_map.Count > _maximumStatsStored)
            {
                // After we remove the highest value, we need to find the new highest
                // We can do that by doing an O(N) operation over the dictionary, and pulling
                // the new high value is O(1) because this is a hash map
                _map.Remove(_highKey); // O(1)
                _highKey = _map.MaxBy(kvp => kvp.Value).Key; // O(N)
                _highValue = _map[_highKey]; // O(1)
            }
        }
    }
}
