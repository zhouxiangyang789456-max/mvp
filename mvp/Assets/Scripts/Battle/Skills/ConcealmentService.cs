using System;
using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Map;
using Mvp.Battle.Units;
using Mvp.Battle.Vision;
using Mvp.Battle.AI;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>
    /// Concealment rules (战斗技能系统开发文档 §6). A group that is entirely stationary
    /// on Forest becomes hidden after a short prepare; hidden groups are filtered out
    /// of enemy vision and AI target lists. Any member moving / leaving Forest / taking
    /// damage / being approached by an enemy breaks concealment, followed by a reveal
    /// lock so the group cannot flicker in and out of hiding.
    ///
    /// The core rules are static and take an explicit <c>now</c> (plus an injectable
    /// near-enemy query) so NUnit tests can drive the state machine with synthetic time
    /// without a running Unity scene. A hidden ConcealmentHost MonoBehaviour supplies
    /// Time.time and the side effects (AI forget / immediate enemy reveal).
    /// </summary>
    public static class ConcealmentService
    {
        /// <summary>§12: concealment enters after this many stationary seconds on Forest.</summary>
        public static float PrepareSeconds = 1.5f;
        /// <summary>§12: after exposure the group cannot re-hide for this many seconds.</summary>
        public static float RevealLockSeconds = 3f;
        /// <summary>§6.4: an enemy within this Chebyshev distance discovers a hidden group.</summary>
        public const int CloseDiscoveryDistance = 1;

        static readonly Dictionary<string, ConcealmentRuntime> _runtime =
            new Dictionary<string, ConcealmentRuntime>();
        static ConcealmentHost _host;

        sealed class ConcealmentRuntime
        {
            public bool Concealed;
            public bool Preparing;
            public float PrepareUntil;
            public float RevealLockUntil;
            public bool HealthInitialized;
            public int LastHealthTotal;
        }

        public struct ConcealmentEvaluation
        {
            public bool Concealed;
            public bool Exposed;
            /// <summary>An enemy stepped adjacent and immediately discovered the group.</summary>
            public bool DiscoveredByEnemy;
            public UnitView NearestEnemy;
        }

        // ---- lifecycle ----------------------------------------------------------

        public static void EnsureHost()
        {
            if (_host != null) return;
            var go = new GameObject("ConcealmentHost");
            _host = go.AddComponent<ConcealmentHost>();
        }

        public static void Shutdown()
        {
            if (_host != null)
            {
                var host = _host;
                _host = null;
                if (host != null) UnityEngine.Object.Destroy(host.gameObject);
            }
            _runtime.Clear();
        }

        /// <summary>Called when a group enters Concealment mode (or switches to it).</summary>
        public static void BeginConcealment(CommanderGroupRuntime group)
        {
            if (group == null) return;
            var rt = GetOrCreate(group.GroupId);
            rt.Concealed = false;
            rt.Preparing = false;
            rt.PrepareUntil = 0f;
            rt.RevealLockUntil = 0f;
            rt.HealthInitialized = false;
            rt.LastHealthTotal = 0;
        }

        /// <summary>Called when the group leaves Concealment mode.</summary>
        public static void EndConcealment(CommanderGroupRuntime group)
        {
            if (group == null) return;
            _runtime.Remove(group.GroupId);
        }

        public static void RemoveGroup(string groupId)
        {
            if (!string.IsNullOrEmpty(groupId)) _runtime.Remove(groupId);
        }

        // ---- queries ------------------------------------------------------------

        public static bool IsConcealed(CommanderGroupRuntime group)
        {
            return group != null && IsConcealed(group.GroupId);
        }

        public static bool IsConcealed(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return false;
            ConcealmentRuntime rt;
            return _runtime.TryGetValue(groupId, out rt) && rt.Concealed;
        }

        public static bool IsInRevealLock(CommanderGroupRuntime group, float now)
        {
            if (group == null) return false;
            ConcealmentRuntime rt;
            return _runtime.TryGetValue(group.GroupId, out rt) && rt.RevealLockUntil > now;
        }

        /// <summary>
        /// §6.2 / §6.1 prerequisites: no move/attack/capture in flight, every alive
        /// member stationary and standing on Forest. No mode requirement (callers
        /// decide whether the mode is being entered or already active).
        /// </summary>
        public static bool MeetsConcealmentPrerequisites(CommanderGroupRuntime group)
        {
            if (group == null || group.IsDefeated) return false;
            if (group.State == CommanderGroupState.Moving ||
                group.State == CommanderGroupState.Attacking ||
                group.State == CommanderGroupState.Regrouping ||
                group.State == CommanderGroupState.Capturing) return false;
            var grid = BattleGridController.Instance;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead)
                    continue;
                if (member.Data.State == UnitState.Moving ||
                    member.Data.State == UnitState.Chasing ||
                    member.Data.State == UnitState.Attacking ||
                    member.Data.State == UnitState.Capturing) return false;
                if (grid == null) return false;
                TerrainType terrain = grid.GetTerrain(member.Data.GridPosition);
                if (!SkillEligibilityService.TerrainMatches(SkillTerrainKind.Forest, terrain))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Core state machine. <paramref name="now"/> is synthetic time (Time.time in
        /// production, an explicit value in tests). <paramref name="nearbyEnemyQuery"/>
        /// defaults to FindNearbyEnemy and may be injected by tests.
        /// </summary>
        public static ConcealmentEvaluation Evaluate(CommanderGroupRuntime group, float now,
            Func<CommanderGroupRuntime, UnitView> nearbyEnemyQuery = null)
        {
            var result = new ConcealmentEvaluation();
            if (group == null || group.Skills.PersistentMode != PersistentSkillMode.Concealment ||
                group.IsDefeated)
            {
                if (group != null) RemoveGroup(group.GroupId);
                return result;
            }
            if (nearbyEnemyQuery == null) nearbyEnemyQuery = FindNearbyEnemy;
            var rt = GetOrCreate(group.GroupId);

            // Reveal lock: can neither be hidden nor start preparing.
            if (now < rt.RevealLockUntil)
            {
                rt.Concealed = false;
                rt.Preparing = false;
                rt.PrepareUntil = 0f;
                result.Concealed = false;
                return result;
            }

            if (!MeetsConcealmentPrerequisites(group))
            {
                rt.Concealed = false;
                rt.Preparing = false;
                rt.PrepareUntil = 0f;
                result.Concealed = false;
                return result;
            }

            // Close-range discovery runs before everything else: an adjacent enemy both
            // breaks concealment and immediately gains vision (§6.4 / 验收 risk #9).
            UnitView nearEnemy = nearbyEnemyQuery(group);
            if (nearEnemy != null)
            {
                result.DiscoveredByEnemy = true;
                result.NearestEnemy = nearEnemy;
                Expose(rt, now);
                result.Concealed = false;
                result.Exposed = true;
                return result;
            }

            if (!rt.Concealed)
            {
                if (!rt.Preparing)
                {
                    rt.Preparing = true;
                    rt.PrepareUntil = now + PrepareSeconds;
                    rt.LastHealthTotal = HealthTotal(group);
                    rt.HealthInitialized = true;
                }
                if (now >= rt.PrepareUntil)
                {
                    rt.Concealed = true;
                    rt.Preparing = false;
                    result.Concealed = true;
                }
                return result;
            }

            // Concealed: damage taken exposes the group.
            int healthTotal = HealthTotal(group);
            if (rt.HealthInitialized && healthTotal < rt.LastHealthTotal)
            {
                Expose(rt, now);
                result.Concealed = false;
                result.Exposed = true;
                return result;
            }
            rt.LastHealthTotal = healthTotal;
            result.Concealed = true;
            return result;
        }

        static void Expose(ConcealmentRuntime rt, float now)
        {
            rt.Concealed = false;
            rt.Preparing = false;
            rt.PrepareUntil = 0f;
            rt.RevealLockUntil = now + RevealLockSeconds;
            rt.HealthInitialized = false;
        }

        /// <summary>Nearest opposing unit within CloseDiscoveryDistance of any alive member.</summary>
        public static UnitView FindNearbyEnemy(CommanderGroupRuntime group)
        {
            if (group == null) return null;
            var spatial = BattleSpatialIndex.Instance;
            var registry = CommanderGroupRegistry.Instance;
            if (spatial == null || registry == null) return null;
            var buffer = new List<UnitView>(8);
            UnitView nearest = null;
            int nearestDist = int.MaxValue;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead)
                    continue;
                buffer.Clear();
                spatial.QueryEnemiesChebyshev(member.Data.GridPosition,
                    CloseDiscoveryDistance, group.Team, buffer);
                for (int b = 0; b < buffer.Count; b++)
                {
                    var enemy = buffer[b];
                    if (enemy == null || enemy.Data == null || enemy.Data.State == UnitState.Dead)
                        continue;
                    var enemyGroup = registry.Find(enemy);
                    if (enemyGroup == null || enemyGroup.Team == group.Team) continue;
                    int dist = Mathf.Max(
                        Mathf.Abs(member.Data.GridPosition.x - enemy.Data.GridPosition.x),
                        Mathf.Abs(member.Data.GridPosition.y - enemy.Data.GridPosition.y));
                    if (dist >= nearestDist) continue;
                    nearest = enemy;
                    nearestDist = dist;
                }
            }
            return nearest;
        }

        static int HealthTotal(CommanderGroupRuntime group)
        {
            int total = 0;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member != null && member.Data != null && member.Data.State != UnitState.Dead)
                    total += member.Data.CurrentHealth;
            }
            return total;
        }

        static ConcealmentRuntime GetOrCreate(string groupId)
        {
            ConcealmentRuntime rt;
            if (!_runtime.TryGetValue(groupId, out rt))
            {
                rt = new ConcealmentRuntime();
                _runtime[groupId] = rt;
            }
            return rt;
        }

        // ---- hidden host --------------------------------------------------------

        sealed class ConcealmentHost : MonoBehaviour
        {
            void OnEnable()
            {
                BattleTickService.MediumTick += TickConcealment;
                var registry = CommanderGroupRegistry.Instance;
                if (registry != null) registry.GroupDefeated += OnGroupDefeated;
            }

            void OnDisable()
            {
                BattleTickService.MediumTick -= TickConcealment;
                var registry = CommanderGroupRegistry.Instance;
                if (registry != null) registry.GroupDefeated -= OnGroupDefeated;
            }

            void OnGroupDefeated(CommanderGroupRuntime group)
            {
                if (group != null) RemoveGroup(group.GroupId);
            }

            void OnDestroy()
            {
                _runtime.Clear();
            }

            void TickConcealment()
            {
                var registry = CommanderGroupRegistry.Instance;
                var ai = EnemyGroupAiController.Instance;
                var vision = BattleVisionService.Instance;
                if (registry == null) return;
                float now = Time.time;
                for (int i = 0; i < registry.Groups.Count; i++)
                {
                    var group = registry.Groups[i];
                    if (group == null || group.Skills.PersistentMode != PersistentSkillMode.Concealment)
                        continue;
                    var eval = Evaluate(group, now, FindNearbyEnemy);
                    if (eval.Concealed && !IsConcealed(group))
                    {
                        // Entering concealment: enemy AI must forget its memory anchor.
                        if (ai != null) ai.ForgetGroup(group);
                    }
                    if (eval.DiscoveredByEnemy && eval.NearestEnemy != null && vision != null)
                    {
                        var enemyGroup = registry.Find(eval.NearestEnemy);
                        if (enemyGroup != null) vision.RevealNow(enemyGroup, group);
                    }
                }
            }
        }
    }
}
