using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Mvp.Battle
{
    public enum PathPriority
    {
        Low = 0,      // chase re-path (throttled)
        Normal = 1,   // move command
        High = 2      // new explicit move command
    }

    public sealed class PathResult
    {
        public bool Success;
        public int Sequence;
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();
    }

    /// <summary>
    /// Throttled path request queue (性能文档：寻路请求队列).
    /// - At most MaxRequestsPerFrame requests processed per frame.
    /// - New request for the same requester overrides the old one.
    /// - Same requester + same target is ignored (no duplicate work).
    /// - Higher priority requests are processed first.
    /// </summary>
    public sealed class PathRequestQueue : MonoBehaviour
    {
        public static PathRequestQueue Instance
        {
            get
            {
                if (_instance == null) BattleCore.Ensure();
                return _instance;
            }
        }
        static PathRequestQueue _instance;
        public static PathRequestQueue ExistingInstance => _instance;

        [SerializeField] int _maxRequestsPerFrame = 2;
        [SerializeField] float _maxMillisecondsPerFrame = 0.5f;

        PathfindingService _pathfinder;
        readonly List<PathRequest> _queue = new List<PathRequest>();

        public PathfindingService Pathfinder => _pathfinder;
        public int PendingCount => _queue.Count;
        public int ProcessedCount { get; private set; }
        public int CancelledCount { get; private set; }

        public int MaxRequestsPerFrame
        {
            get => _maxRequestsPerFrame;
            set => _maxRequestsPerFrame = Mathf.Max(1, value);
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                Clear();
                _instance = null;
            }
        }

        public void Initialize(PathfindingService pathfinder)
        {
            _pathfinder = pathfinder;
        }

        /// <summary>
        /// Queues a path request. If the requester already has a pending request the old one
        /// is replaced unless the target is identical (then it is skipped).
        /// </summary>
        public void Enqueue(object requester, Vector2Int start, Vector2Int target, PathPriority priority,
                            bool allowOccupiedEnd, Action<PathResult> onComplete)
        {
            Enqueue(requester, start, target, priority, allowOccupiedEnd, 0, onComplete);
        }

        /// <summary>
        /// Queues a versioned request. Callers compare PathResult.Sequence with their current
        /// command sequence before applying an asynchronous result.
        /// </summary>
        public void Enqueue(object requester, Vector2Int start, Vector2Int target, PathPriority priority,
                            bool allowOccupiedEnd, int sequence, Action<PathResult> onComplete)
        {
            if (_pathfinder == null)
            {
                Debug.LogWarning("[PathRequestQueue] PathfindingService not initialized; request dropped.");
                return;
            }

            for (int i = 0; i < _queue.Count; i++)
            {
                if (!ReferenceEquals(_queue[i].Requester, requester)) continue;
                if (_queue[i].Target == target && _queue[i].Sequence == sequence) return;
                _queue[i] = new PathRequest(requester, start, target, priority,
                    allowOccupiedEnd, sequence, onComplete);
                return;
            }

            _queue.Add(new PathRequest(requester, start, target, priority,
                allowOccupiedEnd, sequence, onComplete));
        }

        public bool Cancel(object requester)
        {
            if (requester == null) return false;
            bool removed = false;
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_queue[i].Requester, requester)) continue;
                RemoveAtSwapBack(i);
                CancelledCount++;
                removed = true;
            }
            return removed;
        }

        public void Clear()
        {
            CancelledCount += _queue.Count;
            _queue.Clear();
        }

        void Update()
        {
            Flush(_maxRequestsPerFrame);
        }

        /// <summary>
        /// Processes up to <paramref name="max"/> queued requests (highest priority first).
        /// Public so tests can drive it synchronously.
        /// </summary>
        public void Flush(int max)
        {
            if (_pathfinder == null || _queue.Count == 0) return;

            int n = Mathf.Min(Mathf.Max(1, max), _queue.Count);
            long started = Stopwatch.GetTimestamp();
            for (int i = 0; i < n; i++)
            {
                int best = FindHighestPriorityIndex();
                PathRequest req = _queue[best];
                RemoveAtSwapBack(best);

                var result = new PathResult();
                result.Sequence = req.Sequence;
                result.Success = _pathfinder.FindPath(req.Start, req.Target, result.Cells, req.AllowOccupiedEnd);
                req.OnComplete?.Invoke(result);
                ProcessedCount++;

                if (i + 1 < n && ElapsedMilliseconds(started) >= _maxMillisecondsPerFrame)
                    break;
            }
        }

        static double ElapsedMilliseconds(long started)
        {
            return (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        }

        int FindHighestPriorityIndex()
        {
            int best = 0;
            for (int i = 1; i < _queue.Count; i++)
                if (_queue[i].Priority > _queue[best].Priority) best = i;
            return best;
        }

        void RemoveAtSwapBack(int index)
        {
            int last = _queue.Count - 1;
            if (index != last) _queue[index] = _queue[last];
            _queue.RemoveAt(last);
        }

        struct PathRequest
        {
            public object Requester;
            public Vector2Int Start;
            public Vector2Int Target;
            public PathPriority Priority;
            public bool AllowOccupiedEnd;
            public int Sequence;
            public Action<PathResult> OnComplete;

            public PathRequest(object requester, Vector2Int start, Vector2Int target, PathPriority priority,
                               bool allowOccupiedEnd, int sequence, Action<PathResult> onComplete)
            {
                Requester = requester;
                Start = start;
                Target = target;
                Priority = priority;
                AllowOccupiedEnd = allowOccupiedEnd;
                Sequence = sequence;
                OnComplete = onComplete;
            }
        }
    }
}
