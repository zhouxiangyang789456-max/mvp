using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.AI;
using Mvp.Battle.Commanders;
using Mvp.Battle.Map;
using Mvp.Battle.Units;
using Mvp.Battle.Vision;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>Independent, non-selectable tactical target created by 疑兵.</summary>
    public sealed class TacticalDecoyRuntime
    {
        public string DecoyId;
        public string SourceGroupId;
        public TeamId Team;
        public Vector2Int Cell;
        public float ExpiresAt;
        public UnitView AttackProxy;
        public readonly HashSet<string> AffectedGroupIds = new HashSet<string>();

        public bool IsAlive
        {
            get
            {
                return AttackProxy != null && AttackProxy.Data != null &&
                    AttackProxy.Data.State != UnitState.Dead &&
                    AttackProxy.Data.CurrentHealth > 0;
            }
        }
    }

    /// <summary>
    /// Owns decoy placement, cooldown, periodic lure scans and cleanup. Decoys never
    /// enter CommanderGroupRegistry, UnitSelectionController or BattleSpatialIndex.
    /// </summary>
    public static class TacticalDecoyService
    {
        static readonly Dictionary<string, TacticalDecoyRuntime> _byId =
            new Dictionary<string, TacticalDecoyRuntime>();
        static readonly Dictionary<string, string> _bySource =
            new Dictionary<string, string>();
        static readonly Dictionary<string, string> _forcedByGroup =
            new Dictionary<string, string>();
        static readonly Dictionary<string, float> _cooldownUntil =
            new Dictionary<string, float>();
        static readonly List<string> _removeBuffer = new List<string>();
        static int _sequence;
        static TacticalDecoyHost _host;

        public static bool ValidatePlacement(CommanderGroupRuntime source, Vector2Int cell,
            out string reason)
        {
            reason = null;
            var def = SkillCatalog.Get(SkillIds.Decoy);
            var grid = BattleGridController.Instance;
            if (source == null || source.IsDefeated) { reason = "未激活可用编队"; return false; }
            if (BattlePhaseState.Current != BattlePhase.Combat) { reason = "仅在战斗阶段"; return false; }
            if (def == null || grid == null) { reason = "疑兵系统不可用"; return false; }
            if (SkillRangeMath.Chebyshev(source.AnchorCell, cell) > def.RangeCells)
            { reason = "超出施放范围"; return false; }
            if (!grid.InBounds(cell) || !grid.IsWalkable(cell))
            { reason = "目标格不可用"; return false; }
            if (grid.IsOccupied(cell) || FindAtCell(cell) != null)
            { reason = "目标格已被占用"; return false; }
            return true;
        }

        public static bool TryPlace(CommanderGroupRuntime source, Vector2Int cell, float now,
            out int affectedCount, out string reason)
        {
            affectedCount = 0;
            reason = null;
            if (!ValidatePlacement(source, cell, out reason)) return false;
            if (GetRemainingCooldown(source, now) > 0.01f)
            { reason = "疑兵冷却中"; return false; }

            EnsureHost();
            var def = SkillCatalog.Get(SkillIds.Decoy);
            string oldId;
            if (_bySource.TryGetValue(source.GroupId, out oldId)) Remove(oldId);

            var proxy = CreateProxy(source, cell, def.EffectHealth);
            if (proxy == null) { reason = "分身创建失败"; return false; }
            var runtime = new TacticalDecoyRuntime
            {
                DecoyId = "decoy_" + source.GroupId + "_" + (++_sequence),
                SourceGroupId = source.GroupId,
                Team = source.Team,
                Cell = cell,
                ExpiresAt = now + def.DurationSeconds,
                AttackProxy = proxy
            };
            proxy.gameObject.name = runtime.DecoyId;
            _byId[runtime.DecoyId] = runtime;
            _bySource[source.GroupId] = runtime.DecoyId;
            _cooldownUntil[source.GroupId] = now + def.CooldownSeconds;
            BattleGridController.Instance.SetOccupied(cell, true);
            affectedCount = Scan(runtime, def.EffectRangeCells);
            return true;
        }

        public static bool TryGetForcedTarget(CommanderGroupRuntime affected,
            out TacticalDecoyRuntime decoy)
        {
            decoy = null;
            if (affected == null) return false;
            string id;
            if (!_forcedByGroup.TryGetValue(affected.GroupId, out id)) return false;
            if (!_byId.TryGetValue(id, out decoy) || decoy == null || !decoy.IsAlive)
            {
                _forcedByGroup.Remove(affected.GroupId);
                decoy = null;
                return false;
            }
            return true;
        }

        /// <summary>Allows a later direct taunt to override this tactical target.</summary>
        public static void ClearForcedTarget(string affectedGroupId)
        {
            if (string.IsNullOrEmpty(affectedGroupId)) return;
            _forcedByGroup.Remove(affectedGroupId);
            // Keep the id in the decoy's AffectedGroupIds so periodic scans do not
            // immediately re-apply an older effect over the newer taunt.
        }

        public static float GetRemainingCooldown(CommanderGroupRuntime source, float now)
        {
            if (source == null) return 0f;
            float until;
            return _cooldownUntil.TryGetValue(source.GroupId, out until) && until > now
                ? until - now : 0f;
        }

        public static bool IsInRange(Vector2Int source, Vector2Int target, int range)
        {
            return SkillRangeMath.Chebyshev(source, target) <= range;
        }

        public static TacticalDecoyRuntime FindAtCell(Vector2Int cell)
        {
            foreach (var pair in _byId)
                if (pair.Value != null && pair.Value.Cell == cell) return pair.Value;
            return null;
        }

        public static void Shutdown()
        {
            _removeBuffer.Clear();
            foreach (var pair in _byId) _removeBuffer.Add(pair.Key);
            for (int i = 0; i < _removeBuffer.Count; i++) Remove(_removeBuffer[i]);
            _cooldownUntil.Clear();
            if (_host != null) Object.Destroy(_host.gameObject);
            _host = null;
        }

        static void EnsureHost()
        {
            if (_host != null) return;
            var go = new GameObject("TacticalDecoyHost");
            _host = go.AddComponent<TacticalDecoyHost>();
        }

        static UnitView CreateProxy(CommanderGroupRuntime source, Vector2Int cell, int health)
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return null;
            var go = new GameObject("TacticalDecoyProxy");
            var view = go.AddComponent<UnitView>();
            var definition = new UnitDefinition
            {
                Type = UnitType.Infantry,
                DisplayName = "疑兵分身",
                MaxHealth = Mathf.Max(1, health),
                MoveSpeed = 0f,
                VisionRange = 0,
                AttackRangeMax = 0f,
                AttackPower = 0,
                AttackCooldown = 1f,
                Tags = UnitTag.None
            };
            var data = new UnitRuntimeData
            {
                Id = "decoy_proxy_" + (_sequence + 1),
                Team = source.Team,
                Definition = definition,
                CommanderGroupId = null,
                GridPosition = cell,
                CurrentHealth = definition.MaxHealth,
                State = UnitState.Idle
            };
            view.Spawn(data, grid.GridToWorld(cell));
            // Spawn builds a reusable UnitView attack proxy, then it is explicitly
            // removed from normal unit discovery and player selection semantics.
            var selection = UnitSelectionController.Instance;
            if (selection != null) selection.Unregister(view);
            var spatial = BattleSpatialIndex.Instance;
            if (spatial != null) spatial.Unregister(view);
            foreach (var collider in go.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var color = new Color(0.35f, 0.85f, 1f, 0.58f);
                if (renderer.material.HasProperty("_Color")) renderer.material.color = color;
            }
            return view;
        }

        static int Scan(TacticalDecoyRuntime decoy, int range)
        {
            int added = 0;
            var registry = CommanderGroupRegistry.Instance;
            var ai = EnemyGroupAiController.Instance;
            if (registry == null) return 0;
            for (int i = 0; i < registry.Groups.Count; i++)
            {
                var group = registry.Groups[i];
                if (group == null || group.IsDefeated || group.Team == decoy.Team) continue;
                if (SkillRangeMath.Chebyshev(group.AnchorCell, decoy.Cell) > range) continue;
                if (!decoy.AffectedGroupIds.Add(group.GroupId)) continue;
                // A newly applied decoy is newer than any existing direct taunt.
                TauntEffectService.ClearAffected(group.GroupId);
                _forcedByGroup[group.GroupId] = decoy.DecoyId;
                if (ai != null) ai.NotifyDecoyLured(group, decoy);
                added++;
            }
            return added;
        }

        static void Tick(float now)
        {
            var def = SkillCatalog.Get(SkillIds.Decoy);
            if (def == null) return;
            _removeBuffer.Clear();
            foreach (var pair in _byId)
            {
                var decoy = pair.Value;
                if (decoy == null || !decoy.IsAlive || decoy.ExpiresAt <= now)
                    _removeBuffer.Add(pair.Key);
                else
                    Scan(decoy, def.EffectRangeCells);
            }
            for (int i = 0; i < _removeBuffer.Count; i++) Remove(_removeBuffer[i]);
        }

        static void Remove(string decoyId)
        {
            TacticalDecoyRuntime decoy;
            if (string.IsNullOrEmpty(decoyId) || !_byId.TryGetValue(decoyId, out decoy)) return;
            _byId.Remove(decoyId);
            string current;
            if (_bySource.TryGetValue(decoy.SourceGroupId, out current) && current == decoyId)
                _bySource.Remove(decoy.SourceGroupId);
            foreach (var groupId in decoy.AffectedGroupIds)
            {
                string forced;
                if (_forcedByGroup.TryGetValue(groupId, out forced) && forced == decoyId)
                    _forcedByGroup.Remove(groupId);
            }
            var grid = BattleGridController.Instance;
            if (grid != null && grid.InBounds(decoy.Cell)) grid.SetOccupied(decoy.Cell, false);
            if (decoy.AttackProxy != null) decoy.AttackProxy.Die();
        }

        sealed class TacticalDecoyHost : MonoBehaviour
        {
            void OnEnable() { BattleTickService.MediumTick += OnMediumTick; }
            void OnDisable() { BattleTickService.MediumTick -= OnMediumTick; }
            void OnMediumTick() { Tick(Time.time); }
            void OnDestroy()
            {
                if (_host == this) _host = null;
                _byId.Clear();
                _bySource.Clear();
                _forcedByGroup.Clear();
            }
        }
    }
}
