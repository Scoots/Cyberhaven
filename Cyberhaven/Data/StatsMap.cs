using System.Collections.ObjectModel;

namespace Cyberhaven.Data
{
    public interface IStatsMap
    {
        ReadOnlyDictionary<string, long> Stats { get; }
        void Add(string key, long value);
    }

    /// <summary>
    /// Object that would interact with the database on startup and store data on client join
    /// statistics. We don't inherit from Dictionary<string, long> because we want to make sure
    /// the access to the underlying data structure is locked down
    /// </summary>
    internal class StatsMap : IStatsMap
    {
        /// <summary>
        /// The maximum pieces of data that will be stored and returned
        /// </summary>
        private readonly int _maximumStatsStored;

        /// <summary>
        /// The key for the item with the highest value, and its associated value
        /// </summary>
        private string _highKey = string.Empty;
        private long _highValue = long.MinValue;

        /// <summary>
        /// Private dictionary containing our map, wreturns a ReadOnlyDictionary for safety
        /// </summary>
        private readonly Dictionary<string, long> _map = new();
        public ReadOnlyDictionary<string, long> Stats
        { 
            get
            {
                return new ReadOnlyDictionary<string, long>(_map);
            } 
        }

        public StatsMap(CyberhavenConfig config)
        {
            _maximumStatsStored = config.MaxStatCountStored;
            // If we had a db backend we would populate our stats from there here, and then keep a local cache for speed
        }

        public void Add(string key, long value)
        {
            if (_map.ContainsKey(key))
            {
                throw new Exception($"This key has already been added, which should never happen: {key}");
            }

            // If we are at our maximum limit, and the current value is higher than our highest in the list
            // we will just return - we don't need to add anything. If we were backing this with a data store
            // we would still save it to the database
            if (_map.Count >= _maximumStatsStored && value > _highValue)
            {
                return;
            }

            _map[key] = value;

            // Update our high value, this is O(1)
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
