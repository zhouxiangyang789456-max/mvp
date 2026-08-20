using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Units;
using Mvp.Battle.Vision;
using Mvp.Shared;

namespace Mvp.Battle.AI
{
    public enum EnemyAiState
    {
        Dormant,
        AcquireTarget,
        WaitCommand,
        InvestigateLastKnown,
        Recover,
        Defeated
    }

    public enum EnemyAiDecisionReason
    {
        None,
        TargetAcquired,
        TargetStillVisible,
        TargetLost,
        TargetRediscovered,
        ReachedLastKnown,
        MemoryExpired,
        TargetDefeated,
        CommandRejected,
        Recovering
    }

    /// <summary>
    /// Low-frequency enemy group AI. Targets come exclusively from vision snapshots.
    /// Lost targets are investigated at their frozen last-known anchor.
    /// </summary>
    public sealed class EnemyGroupAiController : MonoBehaviour
    {
        [SerializeField] EnemyAiConfig _config = new EnemyAiConfig();

        readonly List<CommanderGroupRuntime> _visibleTargets =
            new List<CommanderGroupRuntime>(8);
        readonly Dictionary<string, EnemyGroupAiRuntime> _contexts =
            new Dictionary<string, EnemyGroupAiRuntime>(8);
        int _groupCursor;
        int _decisionTick;

        public int DecisionCount { get; private set; }
        public int CommandFailureCount { get; private set; }
        public int NoTargetCount { get; private set; }
        public int MemoryInvestigationCount { get; private set; }

        void Awake()
        {
            _config.Sanitize();
        }

        void OnEnable()
        {
            BattleTickService.SlowTick += OnSlowTick;
        }

        void OnDisable()
        {
            BattleTickService.SlowTick -= OnSlowTick;
        }

        void OnDestroy()
        {
            _contexts.Clear();
        }

        public bool TryGetContext(string groupId, out EnemyGroupAiSnapshot snapshot)
        {
            EnemyGroupAiRuntime context;
            if (_contexts.TryGetValue(groupId, out context))
            {
                snapshot = new EnemyGroupAiSnapshot(context);
                return true;
            }
            snapshot = default(EnemyGroupAiSnapshot);
            return false;
        }

        void OnSlowTick()
        {
            _decisionTick++;
            if (BattlePhaseState.Current != BattlePhase.Combat) return;
            var registry = CommanderGroupRegistry.Instance;
            var vision = BattleVisionService.Instance;
            var commands = CommanderGroupCommandController.Instance;
            if (registry == null || vision == null || commands == null ||
                registry.Groups.Count == 0) return;

            CommanderGroupRuntime group = FindNextEnemyGroup(registry);
            if (group == null) return;
            EnemyGroupAiRuntime context = GetOrCreateContext(group);
            if (group.IsDefeated)
            {
                context.State = EnemyAiState.Defeated;
                context.CurrentTargetGroupId = null;
                return;
            }

            DecisionCount++;
            vision.GetVisibleEnemyGroups(group, _visibleTargets);
            CommanderGroupRuntime visibleCurrent = FindById(_visibleTargets,
                context.CurrentTargetGroupId);
            if (visibleCurrent != null)
            {
                RefreshMemory(context, visibleCurrent, vision.GetSnapshotVersion(group));
                HandleVisibleTarget(group, visibleCurrent, context, commands);
                return;
            }

            if (!string.IsNullOrEmpty(context.CurrentTargetGroupId))
            {
                var rememberedTarget = registry.Find(context.CurrentTargetGroupId);
                if (rememberedTarget == null || rememberedTarget.IsDefeated)
                {
                    commands.CancelGroupCommand(group);
                    ClearTarget(context, EnemyAiDecisionReason.TargetDefeated);
                }
                else if (HandleLostTarget(group, context, commands))
                {
                    return;
                }
            }

            if (group.State != CommanderGroupState.Idle) return;
            CommanderGroupRuntime target = SelectTarget(group, _visibleTargets);
            if (target == null)
            {
                NoTargetCount++;
                context.State = EnemyAiState.AcquireTarget;
                return;
            }
            RefreshMemory(context, target, vision.GetSnapshotVersion(group));
            IssueAttack(group, target, context, commands, EnemyAiDecisionReason.TargetAcquired);
        }

        void HandleVisibleTarget(CommanderGroupRuntime group, CommanderGroupRuntime target,
            EnemyGroupAiRuntime context, CommanderGroupCommandController commands)
        {
            bool alreadyAttacking = group.State == CommanderGroupState.Attacking &&
                group.CurrentCommand.TargetGroupId == target.GroupId;
            if (alreadyAttacking)
            {
                context.State = EnemyAiState.WaitCommand;
                context.LastReason = EnemyAiDecisionReason.TargetStillVisible;
                return;
            }

            if (context.State == EnemyAiState.InvestigateLastKnown)
            {
                commands.CancelGroupCommand(group);
                IssueAttack(group, target, context, commands,
                    EnemyAiDecisionReason.TargetRediscovered);
                return;
            }

            if (group.State == CommanderGroupState.Idle)
                IssueAttack(group, target, context, commands,
                    EnemyAiDecisionReason.TargetAcquired);
        }

        bool HandleLostTarget(CommanderGroupRuntime group, EnemyGroupAiRuntime context,
            CommanderGroupCommandController commands)
        {
            if (_decisionTick >= context.MemoryExpireDecisionTick)
            {
                commands.CancelGroupCommand(group);
                ClearTarget(context, EnemyAiDecisionReason.MemoryExpired);
                return false;
            }

            if (_decisionTick < context.RecoverUntilDecisionTick)
            {
                context.State = EnemyAiState.Recover;
                context.LastReason = EnemyAiDecisionReason.Recovering;
                return true;
            }

            if (context.State == EnemyAiState.InvestigateLastKnown)
            {
                if (group.State == CommanderGroupState.Idle)
                {
                    ClearTarget(context, EnemyAiDecisionReason.ReachedLastKnown);
                    return false;
                }
                return true;
            }

            commands.CancelGroupCommand(group);
            if (commands.CommandMove(group, context.LastKnownTargetAnchor))
            {
                context.State = EnemyAiState.InvestigateLastKnown;
                context.LastReason = EnemyAiDecisionReason.TargetLost;
                context.CommandSequence = group.CurrentCommand.Sequence;
                MemoryInvestigationCount++;
                return true;
            }

            CommandFailureCount++;
            context.State = EnemyAiState.Recover;
            context.RecoverUntilDecisionTick = _decisionTick + _config.RecoverDurationTicks;
            context.LastReason = EnemyAiDecisionReason.CommandRejected;
            return true;
        }

        void IssueAttack(CommanderGroupRuntime group, CommanderGroupRuntime target,
            EnemyGroupAiRuntime context, CommanderGroupCommandController commands,
            EnemyAiDecisionReason successReason)
        {
            if (_decisionTick < context.RecoverUntilDecisionTick)
            {
                context.State = EnemyAiState.Recover;
                context.LastReason = EnemyAiDecisionReason.Recovering;
                return;
            }
            UnitView primary = FindPrimaryTarget(group, target);
            if (primary != null && commands.CommandAttack(group, primary))
            {
                context.State = EnemyAiState.WaitCommand;
                context.LastReason = successReason;
                context.ConsecutiveFailureCount = 0;
                context.CommandSequence = group.CurrentCommand.Sequence;
                return;
            }
            CommandFailureCount++;
            context.ConsecutiveFailureCount++;
            context.State = EnemyAiState.Recover;
            context.RecoverUntilDecisionTick = _decisionTick + _config.RecoverDurationTicks;
            context.LastReason = EnemyAiDecisionReason.CommandRejected;
        }

        void RefreshMemory(EnemyGroupAiRuntime context, CommanderGroupRuntime target,
            int perceptionVersion)
        {
            context.CurrentTargetGroupId = target.GroupId;
            context.LastKnownTargetAnchor = target.AnchorCell;
            context.LastSeenDecisionTick = _decisionTick;
            context.MemoryExpireDecisionTick = _decisionTick + _config.MemoryDurationTicks;
            context.PerceptionVersion = perceptionVersion;
        }

        static void ClearTarget(EnemyGroupAiRuntime context, EnemyAiDecisionReason reason)
        {
            context.CurrentTargetGroupId = null;
            context.State = EnemyAiState.AcquireTarget;
            context.LastReason = reason;
            context.LastKnownTargetAnchor = default(Vector2Int);
            context.MemoryExpireDecisionTick = 0;
        }

        EnemyGroupAiRuntime GetOrCreateContext(CommanderGroupRuntime group)
        {
            EnemyGroupAiRuntime context;
            if (!_contexts.TryGetValue(group.GroupId, out context))
            {
                context = new EnemyGroupAiRuntime { State = EnemyAiState.AcquireTarget };
                _contexts[group.GroupId] = context;
            }
            return context;
        }

        CommanderGroupRuntime FindNextEnemyGroup(CommanderGroupRegistry registry)
        {
            int count = registry.Groups.Count;
            for (int checkedCount = 0; checkedCount < count; checkedCount++)
            {
                if (_groupCursor >= count) _groupCursor = 0;
                var group = registry.Groups[_groupCursor++];
                if (group != null && group.Team == TeamId.Enemy) return group;
            }
            return null;
        }

        static CommanderGroupRuntime FindById(List<CommanderGroupRuntime> groups, string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return null;
            for (int i = 0; i < groups.Count; i++)
                if (groups[i].GroupId == groupId) return groups[i];
            return null;
        }

        static CommanderGroupRuntime SelectTarget(CommanderGroupRuntime observer,
            List<CommanderGroupRuntime> candidates)
        {
            CommanderGroupRuntime best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate.IsDefeated || candidate.Team == observer.Team)
                    continue;
                int distance = Manhattan(observer.AnchorCell, candidate.AnchorCell);
                if (distance > bestDistance) continue;
                if (distance == bestDistance && best != null &&
                    string.CompareOrdinal(candidate.GroupId, best.GroupId) >= 0) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        static UnitView FindPrimaryTarget(CommanderGroupRuntime observer,
            CommanderGroupRuntime target)
        {
            if (target == null) return null;
            UnitView best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < target.Members.Count; i++)
            {
                var member = target.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead)
                    continue;
                int distance = Manhattan(observer.AnchorCell, member.Data.GridPosition);
                if (distance >= bestDistance) continue;
                best = member;
                bestDistance = distance;
            }
            return best;
        }

        static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        internal sealed class EnemyGroupAiRuntime
        {
            public EnemyAiState State;
            public string CurrentTargetGroupId;
            public Vector2Int LastKnownTargetAnchor;
            public int LastSeenDecisionTick;
            public int MemoryExpireDecisionTick;
            public int RecoverUntilDecisionTick;
            public int ConsecutiveFailureCount;
            public int PerceptionVersion;
            public int CommandSequence;
            public EnemyAiDecisionReason LastReason;
        }

        public struct EnemyGroupAiSnapshot
        {
            public readonly EnemyAiState State;
            public readonly string CurrentTargetGroupId;
            public readonly Vector2Int LastKnownTargetAnchor;
            public readonly int MemoryExpireDecisionTick;
            public readonly int RecoverUntilDecisionTick;
            public readonly int ConsecutiveFailureCount;
            public readonly int PerceptionVersion;
            public readonly int CommandSequence;
            public readonly EnemyAiDecisionReason LastReason;

            internal EnemyGroupAiSnapshot(EnemyGroupAiRuntime runtime)
            {
                State = runtime.State;
                CurrentTargetGroupId = runtime.CurrentTargetGroupId;
                LastKnownTargetAnchor = runtime.LastKnownTargetAnchor;
                MemoryExpireDecisionTick = runtime.MemoryExpireDecisionTick;
                RecoverUntilDecisionTick = runtime.RecoverUntilDecisionTick;
                ConsecutiveFailureCount = runtime.ConsecutiveFailureCount;
                PerceptionVersion = runtime.PerceptionVersion;
                CommandSequence = runtime.CommandSequence;
                LastReason = runtime.LastReason;
            }
        }
    }
}
