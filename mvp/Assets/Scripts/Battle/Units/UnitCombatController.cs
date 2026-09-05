using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Battle.Vision;
using Mvp.Shared;
using Mvp.Battle.Outcome;
using Mvp.Battle.Traits;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Attack / pursuit controller (战斗页面开发文档: 点击敌方单位→追击→冷却攻击→状态机).
    ///
    /// CommandAttack issues an Attack command: the unit chases the target and, once
    /// within attack range, stops and fires on AttackCooldown. Damage is flat
    /// AttackPower. The target's death ends combat and frees its cell.
    ///
    /// - New move commands cancel combat (via UnitMovementController.CommandMove).
    /// - New attack commands cancel an active move.
    /// - Pursuit re-paths no more often than every 0.3s (性能文档).
    /// - Chase pathing uses allowOccupiedEnd so the final (occupied) target cell is
    ///   never entered; the attacker stops on an adjacent reachable cell.
    /// </summary>
    public sealed class UnitCombatController : MonoBehaviour
    {
        public static UnitCombatController Instance { get; private set; }

        readonly Dictionary<UnitView, CombatState> _combats =
            new Dictionary<UnitView, CombatState>();
        // Fire cadence belongs to the attacker, not to a disposable attack command.
        // It must survive retargeting/cancel-and-reissue so click spam cannot bypass CD.
        readonly Dictionary<UnitView, float> _nextAttackAt =
            new Dictionary<UnitView, float>();
        readonly List<UnitView> _toRemove = new List<UnitView>();
        readonly List<Vector2Int> _pathBuffer = new List<Vector2Int>();
        readonly List<Vector2Int> _bestReach = new List<Vector2Int>();

        PathfindingService _pathfinder;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            BattleTickService.FastTick += OnFastTick;
        }

        void OnDisable()
        {
            BattleTickService.FastTick -= OnFastTick;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var grid = BattleGridController.Instance;
            if (grid != null) _pathfinder = new PathfindingService(grid);
        }

        /// <summary>Issues an attack command. Returns false on reject.</summary>
        public bool CommandAttack(UnitView attacker, UnitView target, bool allowPursuit = true)
        {
            if (BattleSimulationState.IsFrozen) return false;
            if (attacker == null || target == null || attacker.Data == null || target.Data == null)
                return false;
            if (attacker == target) return false;
            if (attacker.Data.State == UnitState.Dead || target.Data.State == UnitState.Dead)
                return false;
            if (attacker.Data.Team == target.Data.Team) return false;

            // New attack command cancels any active move command.
            var move = UnitMovementController.Instance;
            if (move != null) move.CancelMove(attacker);

            CombatState cs;
            if (!_combats.TryGetValue(attacker, out cs))
            {
                cs = new CombatState();
                _combats[attacker] = cs;
            }
            cs.Target = target;
            cs.CooldownDuration = ComputeCooldownDuration(attacker.Data);
            float nextAttackAt;
            cs.Cooldown = _nextAttackAt.TryGetValue(attacker, out nextAttackAt)
                ? Mathf.Max(0f, nextAttackAt - Time.time)
                : 0f;
            cs.RepathTimer = 0f;
            cs.Cells.Clear();
            cs.Index = 0;
            cs.LastTargetCell = target.Data.GridPosition;
            cs.AllowPursuit = allowPursuit;

            var data = attacker.Data;
            data.CurrentCommand.Type = UnitCommandType.Attack;
            data.CurrentCommand.TargetUnit = target.Data;
            data.State = UnitState.Chasing;
            return true;
        }

        /// <summary>
        /// Stationary skill attack (远攻 single target): same as CommandAttack with
        /// allowPursuit=false — the unit never leaves its cell to chase.
        /// </summary>
        public bool CommandSkillAttack(UnitView attacker, UnitView target)
        {
            if (attacker == null || target == null) return false;
            return CommandAttack(attacker, target, false);
        }

        /// <summary>
        /// One-shot area attack at a ground cell (远攻 area units, AreaRadius&gt;0). Damages
        /// every opposing unit within AreaRadius of the cell and shows feedback. The
        /// skill cooldown is managed by the group skill system, so this only applies the
        /// hit and does not enter the normal per-unit chase state.
        /// </summary>
        public bool FireAtCell(UnitView attacker, Vector2Int cell)
        {
            if (BattleSimulationState.IsFrozen) return false;
            if (attacker == null || attacker.Data == null || attacker.Data.Definition == null ||
                attacker.Data.State == UnitState.Dead) return false;
            if (attacker.Data.Definition.AreaRadius <= 0f) return false;
            var grid = BattleGridController.Instance;
            var spatial = BattleSpatialIndex.Instance;
            if (grid == null || spatial == null) return false;
            if (!grid.InBounds(cell)) return false;

            var data = attacker.Data;
            attacker.FaceTowards(GridWorldCenter(grid, cell));
            attacker.PlayAttackAnimation();

            int radius = Mathf.Max(1, Mathf.CeilToInt(data.Definition.AreaRadius));
            var victims = new List<UnitView>(8);
            spatial.QueryEnemiesChebyshev(cell, radius, data.Team, victims);
            for (int i = 0; i < victims.Count; i++)
            {
                var target = victims[i];
                if (target == null || target.Data == null || target.Data.State == UnitState.Dead ||
                    target.Data.ExitState != UnitExitState.Active)
                    continue;
                int dmg = data.Definition.AttackPower;
                dmg = Mathf.RoundToInt(dmg * TraitEffectService.GetAttackPowerMultiplier(data));
                dmg = Mathf.RoundToInt(dmg * TraitEffectService.GetIncomingDamageMultiplier(target.Data));
                if (dmg < 0) dmg = 0;
                target.Data.CurrentHealth -= dmg;
                if (target.Data.CurrentHealth < 0) target.Data.CurrentHealth = 0;
                target.RefreshHealthBar();
                target.FlashHit();
            }
            SpawnAreaFeedback(attacker, grid, cell);
            return true;
        }

        static Vector3 GridWorldCenter(BattleGridController grid, Vector2Int cell)
        {
            var p = grid.GridToWorld(cell);
            p.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell));
            return p;
        }

        void SpawnAreaFeedback(UnitView attacker, BattleGridController grid, Vector2Int cell)
        {
            var pool = EffectPool.Instance;
            if (pool == null) return;
            var muzzle = attacker.transform.position + Vector3.up * 0.7f;
            pool.Get(EffectType.MuzzleFlash, muzzle, Quaternion.identity, 0.12f);
            var hitPos = GridWorldCenter(grid, cell) + Vector3.up * 0.5f;
            pool.Get(EffectType.HitSpark, hitPos, Quaternion.identity, 0.25f);
            pool.Get(EffectType.BulletTracer, Vector3.Lerp(muzzle, hitPos, 0.5f),
                Quaternion.identity, 0.1f);
        }

        /// <summary>Ends any active combat for <paramref name="unit"/> (used by move command).</summary>
        public void CancelCombat(UnitView unit)
        {
            if (unit == null) return;
            _combats.Remove(unit);
            unit.HideAttackCooldownBar();
            var data = unit.Data;
            if (data == null) return;
            if (data.State == UnitState.Chasing || data.State == UnitState.Attacking)
                data.State = UnitState.Idle;
            if (data.CurrentCommand.Type == UnitCommandType.Attack)
            {
                data.CurrentCommand.Type = UnitCommandType.None;
                data.CurrentCommand.TargetUnit = null;
            }
        }

        public bool IsEngaged(UnitView unit)
        {
            return unit != null && _combats.ContainsKey(unit);
        }

        public UnitView GetTarget(UnitView attacker)
        {
            CombatState state;
            return attacker != null && _combats.TryGetValue(attacker, out state)
                ? state.Target
                : null;
        }

        void OnFastTick()
        {
            if (_combats.Count == 0) return;
            float dt = Time.deltaTime;
            _toRemove.Clear();

            foreach (var kv in _combats)
            {
                var attacker = kv.Key;
                var cs = kv.Value;

                if (attacker == null || attacker.Data == null || attacker.Data.State == UnitState.Dead)
                {
                    _toRemove.Add(attacker);
                    continue;
                }

                var target = cs.Target;
                if (target == null || target.Data == null || target.Data.State == UnitState.Dead ||
                    target.Data.ExitState != UnitExitState.Active)
                {
                    EndCombat(attacker);
                    _toRemove.Add(attacker);
                    continue;
                }

                if (InAttackRange(attacker, target))
                {
                    attacker.FaceTowards(target.transform.position);
                    if (attacker.Data.State != UnitState.Attacking)
                    {
                        attacker.Data.State = UnitState.Attacking;
                        SnapToCurrentCell(attacker);
                    }
                    cs.Cooldown -= dt;
                    attacker.SetAttackCooldownFill(1f - (cs.Cooldown / cs.CooldownDuration));
                    if (cs.Cooldown <= 0f)
                    {
                        cs.CooldownDuration = ComputeCooldownDuration(attacker.Data);
                        cs.Cooldown = cs.CooldownDuration;
                        _nextAttackAt[attacker] = Time.time + cs.CooldownDuration;
                        attacker.SetAttackCooldownFill(0f);
                        Fire(attacker, target);
                    }
                }
                else
                {
                    if (!cs.AllowPursuit)
                    {
                        if (attacker.Data.State != UnitState.Idle)
                            attacker.Data.State = UnitState.Idle;
                        attacker.HideAttackCooldownBar();
                        continue;
                    }
                    if (attacker.Data.State != UnitState.Chasing)
                    {
                        attacker.Data.State = UnitState.Chasing;
                        cs.RepathTimer = 0f; // force an immediate re-path on state entry
                        cs.Cells.Clear();
                    }
                    cs.RepathTimer -= dt;
                    Chase(attacker, cs, dt);
                }
            }

            for (int i = 0; i < _toRemove.Count; i++) _combats.Remove(_toRemove[i]);
        }

        bool InAttackRange(UnitView attacker, UnitView target)
        {
            var a = attacker.Data.GridPosition;
            var t = target.Data.GridPosition;
            int dx = Mathf.Abs(a.x - t.x);
            int dz = Mathf.Abs(a.y - t.y);
            return Mathf.Max(dx, dz) <= attacker.Data.Definition.AttackRangeMax + 0.001f;
        }

        void Fire(UnitView attacker, UnitView target)
        {
            if (BattleSimulationState.IsFrozen) return;
            if (target == null || target.Data == null ||
                target.Data.ExitState != UnitExitState.Active) return;
            attacker.PlayAttackAnimation();
            var atk = attacker.Data;
            var tgt = target.Data;
            int dmg = atk.Definition != null ? atk.Definition.AttackPower : 0;
            dmg = Mathf.RoundToInt(dmg * TraitEffectService.GetAttackPowerMultiplier(atk));
            dmg = Mathf.RoundToInt(dmg * TraitEffectService.GetIncomingDamageMultiplier(tgt));
            if (dmg < 0) dmg = 0;
            tgt.CurrentHealth -= dmg;
            if (tgt.CurrentHealth < 0) tgt.CurrentHealth = 0;
            target.RefreshHealthBar();
            target.FlashHit();
            SpawnAttackFeedback(attacker, target);

            if (tgt.CurrentHealth <= 0) Kill(target);
        }

        void SpawnAttackFeedback(UnitView attacker, UnitView target)
        {
            var pool = EffectPool.Instance;
            if (pool == null) return;
            var muzzle = attacker.transform.position + Vector3.up * 0.7f;
            pool.Get(EffectType.MuzzleFlash, muzzle, Quaternion.identity, 0.12f);
            var hitPos = target.transform.position + Vector3.up * 0.6f;
            pool.Get(EffectType.HitSpark, hitPos, Quaternion.identity, 0.18f);
            pool.Get(EffectType.BulletTracer, Vector3.Lerp(muzzle, hitPos, 0.5f),
                Quaternion.identity, 0.1f);
        }

        void Kill(UnitView unit)
        {
            var data = unit.Data;
            if (data == null) return;
            data.State = UnitState.Dead;
            data.CurrentCommand.Type = UnitCommandType.None;
            data.CurrentCommand.TargetUnit = null;

            var grid = BattleGridController.Instance;
            if (grid != null) grid.SetOccupied(data.GridPosition, false);
            var move = UnitMovementController.Instance;
            if (move != null) move.CancelMove(unit);

            unit.Die();
        }

        void Chase(UnitView attacker, CombatState cs, float dt)
        {
            var data = attacker.Data;
            var grid = BattleGridController.Instance;
            if (data == null || grid == null || _pathfinder == null) return;

            Vector2Int targetCell = cs.Target.Data.GridPosition;
            bool needPath = cs.Cells.Count == 0 || cs.Index >= cs.Cells.Count
                || cs.LastTargetCell != targetCell || cs.RepathTimer <= 0f;

            if (needPath)
            {
                cs.RepathTimer = 0.3f; // 性能文档: 追击重新寻路间隔 ≥ 0.3s
                cs.LastTargetCell = targetCell;
                if (!ComputeChasePath(data.GridPosition, targetCell, cs.Cells))
                {
                    EndCombat(attacker); // unreachable: stop at nearest reachable point
                    return;
                }
                cs.Index = 0;
            }

            if (cs.Index >= cs.Cells.Count)
            {
                EndCombat(attacker);
                return;
            }

            float step = data.Definition.MoveSpeed *
                TraitEffectService.GetMoveSpeedMultiplier(data) * dt;
            Vector2Int cell = cs.Cells[cs.Index];
            Vector3 tpos = GridToWorldWithElevation(cell);
            Vector3 pos = attacker.transform.position;
            float dx = tpos.x - pos.x;
            float dz = tpos.z - pos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            if (dist <= CombatState.ArrivalTolerance)
            {
                if (!CanEnterCell(attacker, cell)) return;
                var move = UnitMovementController.Instance;
                if (move != null) move.SnapToCell(attacker, cell);
                else SnapToCellLocal(attacker, cell);
                cs.Index++;
            }
            else
            {
                pos.x += dx / dist * step;
                pos.z += dz / dist * step;
                pos.y = UnitMovementController.GetSafeTraversalElevation(
                    data.GridPosition, cell);
                attacker.transform.position = pos;
                attacker.FaceDirection(tpos - pos);
            }
        }

        bool CanEnterCell(UnitView unit, Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            if (grid == null || unit == null || unit.Data == null) return false;
            if (!grid.IsOccupied(cell)) return true;

            var selection = UnitSelectionController.Instance;
            var occupant = selection != null ? selection.FindAtCell(cell) : null;
            if (occupant == unit) return true;
            return occupant == null || occupant.Data == null ||
                occupant.Data.State == UnitState.Dead;
        }

        /// <summary>
        /// Paths toward the target but never steps onto its occupied cell: the last
        /// waypoint (the target cell) is dropped. Falls back to the nearest reachable
        /// neighbor when the target is on an unreachable island.
        /// </summary>
        bool ComputeChasePath(Vector2Int start, Vector2Int targetCell, List<Vector2Int> cells)
        {
            cells.Clear();
            if (_pathfinder.FindPath(start, targetCell, _pathBuffer, true) && _pathBuffer.Count >= 2)
            {
                for (int i = 1; i < _pathBuffer.Count - 1; i++) cells.Add(_pathBuffer[i]);
                if (cells.Count > 0) return true;
            }
            return FindNearestReachable(start, targetCell, cells);
        }

        bool FindNearestReachable(Vector2Int start, Vector2Int targetCell, List<Vector2Int> cells)
        {
            cells.Clear();
            var grid = BattleGridController.Instance;
            if (grid == null || _pathfinder == null) return false;

            int bestCount = int.MaxValue;
            bool found = false;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var nb = new Vector2Int(targetCell.x + dx, targetCell.y + dz);
                    if (!grid.InBounds(nb) || !grid.IsWalkable(nb)) continue;
                    if (grid.IsOccupied(nb)) continue;
                    if (!_pathfinder.FindPath(start, nb, _pathBuffer, false)) continue;
                    if (_pathBuffer.Count < bestCount)
                    {
                        bestCount = _pathBuffer.Count;
                        _bestReach.Clear();
                        for (int i = 1; i < _pathBuffer.Count; i++) _bestReach.Add(_pathBuffer[i]);
                        found = true;
                    }
                }
            }
            if (!found || _bestReach.Count == 0) return false;
            cells.Clear();
            for (int i = 0; i < _bestReach.Count; i++) cells.Add(_bestReach[i]);
            return true;
        }

        void SnapToCurrentCell(UnitView attacker)
        {
            var move = UnitMovementController.Instance;
            if (move != null) move.SnapToCell(attacker, attacker.Data.GridPosition);
        }

        void SnapToCellLocal(UnitView unit, Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            var data = unit.Data;
            if (grid == null || data == null) return;
            if (data.GridPosition != cell)
            {
                Vector2Int old = data.GridPosition;
                grid.SetOccupied(old, false);
                grid.SetOccupied(cell, true);
                data.GridPosition = cell;
                var sel = UnitSelectionController.Instance;
                if (sel != null) sel.UpdateCell(unit, old, cell);
            }
            var p = grid.GridToWorld(cell);
            p.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell));
            unit.transform.position = p;
        }

        Vector3 GridToWorldWithElevation(Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return new Vector3(cell.x, 0f, cell.y);
            var p = grid.GridToWorld(cell);
            p.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell));
            return p;
        }

        void EndCombat(UnitView attacker)
        {
            var data = attacker.Data;
            if (data != null && (data.State == UnitState.Chasing || data.State == UnitState.Attacking))
                data.State = UnitState.Idle;
            if (data != null && data.CurrentCommand.Type == UnitCommandType.Attack)
            {
                data.CurrentCommand.Type = UnitCommandType.None;
                data.CurrentCommand.TargetUnit = null;
            }
            attacker.HideAttackCooldownBar();
        }

        static float ComputeCooldownDuration(UnitRuntimeData data)
        {
            if (data == null || data.Definition == null) return 1f;
            return Mathf.Max(0.01f, data.Definition.AttackCooldown *
                TraitEffectService.GetAttackCooldownMultiplier(data));
        }

        sealed class CombatState
        {
            public const float ArrivalTolerance = 0.08f;
            public UnitView Target;
            public float Cooldown;
            public float CooldownDuration;
            public float RepathTimer;
            public Vector2Int LastTargetCell;
            public readonly List<Vector2Int> Cells = new List<Vector2Int>();
            public int Index;
            public bool AllowPursuit;
        }

    }
}
