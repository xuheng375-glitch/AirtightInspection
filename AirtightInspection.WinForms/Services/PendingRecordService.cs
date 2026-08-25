using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AirtightInspection.Models;

namespace AirtightInspection.Services
{
    public sealed class PendingRecordService
    {
        private readonly ConcurrentDictionary<int, PendingRecord> _items = new ConcurrentDictionary<int, PendingRecord>();
        public event EventHandler Changed;

        public IReadOnlyList<PendingRecord> Snapshot() => _items.Values.OrderBy(x => x.StationNo).ToList();
        public bool TryGet(int stationNo, out PendingRecord record) => _items.TryGetValue(stationNo, out record);

        public void AddOrReplace(PendingRecord record)
        {
            _items.AddOrUpdate(record.StationNo, record, (_, __) => record);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public bool TryRemove(int stationNo, PendingRecord expected)
        {
            PendingRecord current;
            if (!_items.TryGetValue(stationNo, out current) || !ReferenceEquals(current, expected)) return false;
            var removed = _items.TryRemove(stationNo, out current);
            if (removed) Changed?.Invoke(this, EventArgs.Empty);
            return removed;
        }

        public List<PendingRecord> RemoveExpired(int timeoutSeconds)
        {
            var removed = new List<PendingRecord>();
            if (timeoutSeconds <= 0) return removed;
            var threshold = DateTime.Now.AddSeconds(-timeoutSeconds);
            foreach (var item in _items.Values.Where(x => x.ScanTime < threshold))
                if (TryRemove(item.StationNo, item)) removed.Add(item);
            return removed;
        }
    }
}
