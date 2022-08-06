namespace Cyberhaven.Data
{
    internal class StatsMap
    {
        public Dictionary<string, long> Stats
        { 
            get
            {
                return _map;
            } 
        }

        private readonly Dictionary<string, long> _map = new();
        private readonly int _maximumStatsStored;
        
        private string _highKey = string.Empty;
        private long _highValue = long.MinValue;

        public StatsMap(int maximumStatsStored)
        {
            _maximumStatsStored = maximumStatsStored;
        }


        public bool ContainsKey(string key)
        {
            return _map.ContainsKey(key);
        }

        public void Add(string key, long value)
        {
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
