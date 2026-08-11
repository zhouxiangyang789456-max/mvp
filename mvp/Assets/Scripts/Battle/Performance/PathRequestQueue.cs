using System;
using System.Collections.Generic;
using UnityEngine;

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

        [SerializeField] int _maxRequestsPerFrame = 2;

        PathfindingService _pathfinder;
        readonly List<PathRequest> _queue = new List<PathRequest>();

        public PathfindingService Pathfinder => _pathfinder;
        public int PendingCount => _queue.Count;

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
            if (_instance == this) _instance = null;
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
            if (_pathfinder == null)
            {
                Debug.LogWarning("[PathRequestQueue] PathfindingService not initialized; request dropped.");
                return;
            }

            for (int i = 0; i < _queue.Count; i++)
            {
                if (!ReferenceEquals(_queue[i].Requester, requester)) continue;
                if (_queue[i].Target == target) return; // target unchanged -> skip
                _queue[i] = new PathRequest(requester, start, target, priority, allowOccupiedEnd, onComplete);
                return;
            }

            _queue.Add(new PathRequest(requester, start, target, priority, allowOccupiedEnd, onComplete));
        }

        public void Clear()
        {
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
            if (_queue.Count > 1) _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            int n = Mathf.Min(Mathf.Max(1, max), _queue.Count);
            for (int i = 0; i < n; i++)
            {
                PathRequest req = _queue[0];
                _queue.RemoveAt(0);

                var result = new PathResult();
                result.Success = _pathfinder.FindPath(req.Start, req.Target, result.Cells, req.AllowOccupiedEnd);
                req.OnComplete?.Invoke(result);
            }
        }

        struct PathRequest
        {
            public object Requester;
            public Vector2Int Start;
            public Vector2Int Target;
            public PathPriority Priority;
            public bool AllowOccupiedEnd;
            public Action<PathResult> OnComplete;

            public PathRequest(object requester, Vector2Int start, Vector2Int target, PathPriority priority,
                               bool allowOccupiedEnd, Action<PathResult> onComplete)
            {
                Requester = requester;
                Start = start;
                Target = target;
                Priority = priority;
                AllowOccupiedEnd = allowOccupiedEnd;
                OnComplete = onComplete;
            }
        }
    }
}
