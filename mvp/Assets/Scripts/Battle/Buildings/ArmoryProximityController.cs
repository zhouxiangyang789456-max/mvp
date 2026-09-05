using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Outcome;
using Mvp.Battle.UI;
using Mvp.Battle.Units;
using Mvp.Shared;

namespace Mvp.Battle.Buildings
{
    /// <summary>
    /// Opens an owned armory once when the active player group enters its adjacent ring.
    /// </summary>
    public sealed class ArmoryProximityController : MonoBehaviour
    {
        string _insideGroupId;
        int _insideArmoryId;

        void OnEnable()
        {
            BattleTickService.MediumTick += OnMediumTick;
        }

        void OnDisable()
        {
            BattleTickService.MediumTick -= OnMediumTick;
        }

        void OnMediumTick()
        {
            if (BattleSimulationState.IsFrozen) return;
            var formation = Mvp.Battle.Formation.FormationController.Instance;
            if (formation != null && (formation.IsDeploying || formation.IsCombatEditing))
            {
                ClearInside();
                return;
            }

            var groups = CommanderGroupRegistry.Instance;
            var registry = BuildingRegistry.Instance;
            var group = groups != null ? groups.ActiveGroup : null;
            if (registry == null || group == null || group.IsDefeated || group.Team != TeamId.Player)
            {
                ClearInside();
                return;
            }

            BuildingRuntime nearby = FindNearbyOwnedArmory(group, registry);
            if (nearby == null)
            {
                ClearInside();
                return;
            }

            if (_insideGroupId == group.GroupId && _insideArmoryId == nearby.InstanceId) return;
            _insideGroupId = group.GroupId;
            _insideArmoryId = nearby.InstanceId;
            ArmoryProductionPanel.Show(nearby);
            var status = BattleUiStatusText.Instance;
            if (status != null) status.SetStatus("已进入己方兵工厂生产范围");
        }

        static BuildingRuntime FindNearbyOwnedArmory(CommanderGroupRuntime group,
            BuildingRegistry registry)
        {
            for (int i = 0; i < registry.AllBuildings.Count; i++)
            {
                var building = registry.AllBuildings[i];
                if (building == null || building.Type != BuildingType.Armory ||
                    building.Owner != BuildingOwner.Player || !building.IsOperational) continue;

                for (int j = 0; j < group.Members.Count; j++)
                {
                    var member = group.Members[j];
                    if (member == null || member.Data == null || member.Data.State == UnitState.Dead)
                        continue;
                    if (IsOnAdjacentRing(member.Data.GridPosition, building)) return building;
                }
            }
            return null;
        }

        static bool IsOnAdjacentRing(Vector2Int cell, BuildingRuntime building)
        {
            int minX = building.AnchorCell.x - 1;
            int maxX = building.AnchorCell.x + building.Footprint.x;
            int minY = building.AnchorCell.y - 1;
            int maxY = building.AnchorCell.y + building.Footprint.y;
            if (cell.x < minX || cell.x > maxX || cell.y < minY || cell.y > maxY) return false;
            return cell.x == minX || cell.x == maxX || cell.y == minY || cell.y == maxY;
        }

        void ClearInside()
        {
            _insideGroupId = null;
            _insideArmoryId = 0;
        }
    }
}
