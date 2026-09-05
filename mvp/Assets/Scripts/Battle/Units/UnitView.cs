using System.Collections;
using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Shared;
using Mvp.Battle.Commanders;
using Mvp.Battle.Outcome;
using Mvp.Battle.Vision;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Visual facade for one spawned unit. Builds a placeholder model per the
    /// 回退策略 (正式模型未完成：先用胶囊体/方块占位) — Infantry = capsule,
    /// Tank = box — tinted by team, plus a pooled world-space health bar.
    /// Exposes the anchors that movement/combat/facing controllers drive later.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        public const float PlayerColorR = 0.184f;   // #2F9FE8
        public const float PlayerColorG = 0.624f;
        public const float PlayerColorB = 0.910f;
        public const float EnemyColorR = 0.851f;    // #D94B45
        public const float EnemyColorG = 0.294f;
        public const float EnemyColorB = 0.271f;

        const int InfantryVisualMemberCount = 3;
        const int MaxInfantryVisualMembersPerCell = 3;
        const float InfantryVisualSpacing = 1.2f;
        const float RocketArtilleryGroundClearance = 0.18f;

        public UnitRuntimeData Data { get; private set; }
        public Transform ModelRoot { get; private set; }
        public Transform HealthAnchor { get; private set; }

        UnitHealthBar _healthBar;
        UnitHealthBar _attackCooldownBar;
        Renderer _modelRenderer;
        Color _baseColor;
        Color _normalTint;
        Renderer[] _modelRenderers;
        int _modelSortingOrder = int.MinValue;
        float _flashTimer;
        bool _removedFromBattle;
        GameObject _model;
        GameObject[] _flagRoots = new GameObject[0];
        UnitAnimationDriver[] _animationDrivers = new UnitAnimationDriver[0];
        VehicleUnitAnimationDriver[] _vehicleAnimationDrivers = new VehicleUnitAnimationDriver[0];
        float _modelFacingYawOffset;

        public bool IsFlashing { get; private set; }

        /// <summary>Sets up the placeholder model and health bar at <paramref name="worldPos"/>.</summary>
        public void Spawn(UnitRuntimeData data, Vector3 worldPos)
        {
            Data = data;
            data.WorldPosition = worldPos;
            var grid = BattleGridController.Instance;
            if (grid != null) data.GridPosition = grid.WorldToGrid(worldPos);

            transform.position = worldPos;

            if (UnitSelectionController.Instance != null)
            {
                UnitSelectionController.Instance.Register(this);
            }
            if (BattleSpatialIndex.Instance != null) BattleSpatialIndex.Instance.Register(this);

            ModelRoot = new GameObject("ModelRoot").transform;
            ModelRoot.SetParent(transform, false);

            // Pick a real model when its prefab exists; otherwise fall back to the
            // placeholder. Infantry archetypes use the soldier; Tank/HeavyTank use
            // the static tank model. Both are scaled to remain readable inside a
            // single occupied grid cell.
            string modelPath = null;
            float modelScale = 0.72f;      // placeholder default
            float modelAnchorY = 0.95f;    // placeholder default
            bool useRealModel = false;

            if (data.Definition != null)
            {
                if (data.Definition.Type == UnitType.Scout
                    && Resources.Load<GameObject>("Battle/Units/Scout") != null)
                {
                    // Scout is a lone soldier: single model, infantry-scale.
                    useRealModel = true;
                    modelPath = "Battle/Units/Scout";
                    modelScale = 0.25f;    // ~1.7m soldier at 25% -> ~0.43 units tall
                    modelAnchorY = 0.50f;  // keep the health bar just above the model
                }
                else if (data.Definition.Type == UnitType.ScoutCar
                         && Resources.Load<GameObject>("Battle/Units/ScoutCar") != null)
                {
                    useRealModel = true;
                    modelPath = "Battle/Units/ScoutCar";
                    modelScale = 0.58f;    // ~1.5m long at 58% -> ~0.87 units, fits one cell
                    modelAnchorY = 0.72f;  // health bar floats above the scaled hull
                }
                else if (data.Definition.Type == UnitType.RocketArtillery
                         && Resources.Load<GameObject>("Battle/Units/RocketArtillery") != null)
                {
                    useRealModel = true;
                    modelPath = "Battle/Units/RocketArtillery";
                    modelScale = 0.58f;    // ~1.96m long at 58% -> ~1.14 units, fits one cell
                    modelAnchorY = 0.72f;  // health bar floats above the scaled hull
                }
                else if ((data.Definition.Tags & UnitTag.Infantry) != 0
                    && Resources.Load<GameObject>("Battle/Units/Infantry") != null)
                {
                    useRealModel = true;
                    modelPath = "Battle/Units/Infantry";
                    modelScale = 0.25f;    // 50% of the previous infantry visual size
                    modelAnchorY = 0.50f;  // keep the health bar just above the smaller model
                }
                else if ((data.Definition.Type == UnitType.Tank
                          || data.Definition.Type == UnitType.HeavyTank)
                         && Resources.Load<GameObject>("Battle/Units/Tank") != null)
                {
                    useRealModel = true;
                    modelPath = "Battle/Units/Tank";
                    modelScale = 0.58f;    // ~1.14 units long: fits one grid cell
                    modelAnchorY = 0.72f;  // health bar floats above the scaled hull
                }
            }

            UnitModelProfile modelProfile = null;
            if (useRealModel)
            {
                var selectedPrefab = Resources.Load<GameObject>(modelPath);
                if (selectedPrefab != null)
                {
                    modelProfile = selectedPrefab.GetComponent<UnitModelProfile>();
                    if (modelProfile != null)
                    {
                        modelScale = modelProfile.ModelScale;
                        modelAnchorY = modelProfile.HealthAnchorY;
                    }
                }
            }

            ModelRoot.localScale = Vector3.one * (useRealModel ? modelScale : 0.72f);

            if (useRealModel) BuildModel(data, data.Team, modelPath, modelProfile);
            else BuildPlaceholder(data.Definition.Type, data.Team);
            ConfigureModelRendering();

            HealthAnchor = new GameObject("HealthBarAnchor").transform;
            HealthAnchor.SetParent(transform, false);
            HealthAnchor.localPosition = new Vector3(0f, useRealModel ? modelAnchorY : 0.95f, 0f);
        }

        public void AttachHealthBar()
        {
            if (_healthBar != null) return;
            if (HealthAnchor == null) return;
            var mgr = UnitHealthBarManager.Instance;
            if (mgr == null) return;

            var team = Data != null ? Data.Team : TeamId.Player;
            var barColor = team == TeamId.Player
                ? new Color(0.271f, 0.722f, 0.420f)   // #45B86B
                : new Color(0.851f, 0.294f, 0.271f);  // #D94B45
            _healthBar = mgr.Acquire(HealthAnchor, Vector3.zero, barColor);
            if (_healthBar != null && Data != null)
            {
                int maxHp = RuntimeMaxHealthOf(Data);
                _healthBar.SetFill(maxHp > 0 ? (float)Data.CurrentHealth / maxHp : 1f);
            }
        }

        public void RefreshHealthBar()
        {
            if (_healthBar == null || Data == null) return;
            int maxHp = RuntimeMaxHealthOf(Data);
            _healthBar.SetFill(maxHp > 0 ? (float)Data.CurrentHealth / maxHp : 1f);
        }

        public void ReleaseHealthBar()
        {
            if (_healthBar == null) return;
            var mgr = UnitHealthBarManager.Instance;
            if (mgr != null) mgr.Release(_healthBar);
            _healthBar = null;
        }

        public void SetAttackCooldownFill(float fill01)
        {
            if (_attackCooldownBar == null)
            {
                if (HealthAnchor == null) return;
                var mgr = UnitHealthBarManager.Instance;
                if (mgr == null) return;
                _attackCooldownBar = mgr.Acquire(HealthAnchor, new Vector3(0f, -0.18f, 0f),
                    new Color(1f, 0.78f, 0.2f, 0.95f));
            }
            _attackCooldownBar.SetFill(fill01);
        }

        public void HideAttackCooldownBar()
        {
            if (_attackCooldownBar == null) return;
            var mgr = UnitHealthBarManager.Instance;
            if (mgr != null) mgr.Release(_attackCooldownBar);
            _attackCooldownBar = null;
        }

        void OnDestroy()
        {
            RemoveFromBattleServices();
        }

        static int RuntimeMaxHealthOf(UnitRuntimeData data)
        {
            if (data == null) return 1;
            return data.RuntimeMaxHealth > 0
                ? data.RuntimeMaxHealth
                : (data.Definition != null ? data.Definition.MaxHealth : 1);
        }

        void BuildPlaceholder(UnitType type, TeamId team)
        {
            GameObject prim;
            float baseY;
            if (type == UnitType.Tank)
            {
                prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prim.name = "TankBody";
                prim.transform.SetParent(ModelRoot, false);
                prim.transform.localScale = new Vector3(0.9f, 0.4f, 1.2f);
                baseY = 0.2f;
            }
            else
            {
                prim = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                prim.name = "Infantry";
                prim.transform.SetParent(ModelRoot, false);
                // The unscaled capsule is 2 tall; 0.5 on Y makes it a ~1-unit soldier.
                prim.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                baseY = 0.5f;
            }

            var renderer = prim.GetComponent<Renderer>();
            if (renderer != null)
            {
                _modelRenderer = renderer;
                _baseColor = team == TeamId.Player
                    ? new Color(PlayerColorR, PlayerColorG, PlayerColorB)
                    : new Color(EnemyColorR, EnemyColorG, EnemyColorB);
                _normalTint = _baseColor;
                renderer.material.color = _normalTint;
            }

            prim.transform.localPosition = new Vector3(0f, baseY, 0f);
        }

        void BuildModel(UnitRuntimeData data, TeamId team, string prefabPath,
            UnitModelProfile profile)
        {
            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                BuildPlaceholder(data.Definition.Type, team);
                return;
            }

            // Static/single-model units (tank + the new Blender units) render one
            // model per cell; only the animated infantry archetype renders a squad.
            bool isSingleModel = profile != null ? profile.SingleVisualPerSlot :
                data.Definition.Type == UnitType.Tank ||
                data.Definition.Type == UnitType.HeavyTank ||
                data.Definition.Type == UnitType.Scout ||
                data.Definition.Type == UnitType.ScoutCar;
            int configuredMemberCount = data.MembersPerSlot > 0
                ? data.MembersPerSlot
                : InfantryVisualMemberCount;
            int visualMemberCount = isSingleModel
                ? 1
                : Mathf.Clamp(configuredMemberCount, 1, MaxInfantryVisualMembersPerCell);

            bool isVehicleGroup = data.Definition.Type == UnitType.RocketArtillery;
            _model = new GameObject(isSingleModel ? "UnitModel" :
                isVehicleGroup ? "VehicleVisualGroup" : "InfantryVisualSquad");
            _model.transform.SetParent(ModelRoot, false);
            _model.transform.localScale = Vector3.one;
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = profile != null
                ? Quaternion.Euler(profile.ContainerEuler)
                : isSingleModel || isVehicleGroup
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;

            for (int i = 0; i < visualMemberCount; i++)
            {
                var member = Instantiate(prefab, _model.transform, false);
                member.name = isSingleModel ? "UnitModel" :
                    isVehicleGroup ? "RocketArtillery_" + (i + 1) :
                    "InfantryMember_" + (i + 1);
                member.transform.localScale = Vector3.one;
                member.transform.localPosition = isSingleModel
                    ? Vector3.zero
                    : isVehicleGroup
                        ? VehicleVisualOffset(i, visualMemberCount)
                        : InfantryVisualOffset(i, visualMemberCount);
                // Apply the Blender-to-Unity correction explicitly so deployment
                // and later facing updates always start from the same flat pose.
                member.transform.localRotation = profile != null
                    ? Quaternion.Euler(profile.InstanceEuler)
                    : isVehicleGroup
                    ? Quaternion.Euler(-90f, 0f, 0f)
                    : Quaternion.identity;
                float groundClearance = profile != null
                    ? profile.GroundClearance
                    : isVehicleGroup
                    ? RocketArtilleryGroundClearance
                    : isSingleModel ? 0.12f : 0.05f;
                AlignModelBottomToGround(member, groundClearance);
            }

            // Current battle unit assets use local +X as their visual front;
            // Unity's LookRotation targets local +Z.
            _modelFacingYawOffset = profile != null ? profile.FacingYawOffset : 90f;

            _baseColor = team == TeamId.Player
                ? new Color(PlayerColorR, PlayerColorG, PlayerColorB)
                : new Color(EnemyColorR, EnemyColorG, EnemyColorB);
            _normalTint = Color.Lerp(Color.white, _baseColor, 0.1f);
            TintModel(_normalTint);

            Renderer rend = _model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (rend == null) rend = _model.GetComponentInChildren<MeshRenderer>();
            if (rend != null) _modelRenderer = rend;

            _animationDrivers = _model.GetComponentsInChildren<UnitAnimationDriver>(true);
            for (int i = 0; i < _animationDrivers.Length; i++)
                if (_animationDrivers[i] != null)
                    _animationDrivers[i].Initialize(this);

            _vehicleAnimationDrivers =
                _model.GetComponentsInChildren<VehicleUnitAnimationDriver>(true);
            for (int i = 0; i < _vehicleAnimationDrivers.Length; i++)
                if (_vehicleAnimationDrivers[i] != null)
                    _vehicleAnimationDrivers[i].Initialize(this);

            var flags = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < _model.transform.childCount; i++)
            {
                var flag = _model.transform.GetChild(i).Find("Flag_Root");
                if (flag != null) flags.Add(flag.gameObject);
            }
            _flagRoots = flags.ToArray();
        }

        static Vector3 InfantryVisualOffset(int index, int count)
        {
            float spacing = InfantryVisualSpacing;
            if (count <= 1) return Vector3.zero;
            if (count == 2)
                return new Vector3(index == 0 ? -spacing * 0.5f : spacing * 0.5f, 0f, 0f);
            if (count == 3)
            {
                float depth = spacing * 0.288675f;
                if (index == 0) return new Vector3(-spacing * 0.5f, 0f, -depth);
                if (index == 1) return new Vector3(spacing * 0.5f, 0f, -depth);
                return new Vector3(0f, 0f, depth * 2f);
            }
            if (count == 4)
            {
                float half = spacing * 0.5f;
                return new Vector3(index % 2 == 0 ? -half : half, 0f,
                    index < 2 ? -half : half);
            }

            // Five members: one in the centre and four at equal diagonal offsets.
            if (index == 0) return Vector3.zero;
            float corner = spacing * 0.5f;
            int cornerIndex = index - 1;
            return new Vector3(cornerIndex % 2 == 0 ? -corner : corner, 0f,
                cornerIndex < 2 ? -corner : corner);
        }

        static Vector3 VehicleVisualOffset(int index, int count)
        {
            if (count <= 1) return Vector3.zero;

            // Rocket artillery uses a corrected Blender root whose local X axis
            // follows the vehicle length. Separate vehicles on local Z so they
            // form a left/right row perpendicular to their shared heading.
            float spacing = InfantryVisualSpacing;
            if (count == 2)
                return new Vector3(0f, 0f,
                    index == 0 ? -spacing * 0.5f : spacing * 0.5f);

            return new Vector3(0f, 0f, (index - (count - 1) * 0.5f) * spacing);
        }
        static void AlignModelBottomToGround(GameObject model, float groundClearance)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            float minY = float.PositiveInfinity;
            for (int i = 0; i < renderers.Length; i++)
                minY = Mathf.Min(minY, renderers[i].bounds.min.y);
            if (float.IsInfinity(minY)) return;

            float groundY = model.transform.parent.position.y;
            model.transform.position += Vector3.up * (groundY + groundClearance - minY);
        }
        void ConfigureModelRendering()
        {
            _modelRenderers = ModelRoot != null
                ? ModelRoot.GetComponentsInChildren<Renderer>(true)
                : null;
            if (_modelRenderers == null) return;
            for (int i = 0; i < _modelRenderers.Length; i++)
            {
                var renderer = _modelRenderers[i];
                if (renderer == null) continue;
                renderer.material.renderQueue = 3000;
            }
            RefreshModelSortingOrder();
        }

        void RefreshModelSortingOrder()
        {
            if (_modelRenderers == null) return;
            int depth = Mathf.CeilToInt(transform.position.x) +
                Mathf.CeilToInt(transform.position.z);
            int sortingOrder = depth * 2 + 2;
            if (sortingOrder == _modelSortingOrder) return;
            _modelSortingOrder = sortingOrder;
            for (int i = 0; i < _modelRenderers.Length; i++)
                if (_modelRenderers[i] != null)
                    _modelRenderers[i].sortingOrder = sortingOrder;
        }


        void TintModel(Color color)
        {
            var rends = _model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                // The rifle keeps its own material; everything else is team-tinted.
                if (rends[i].name == "Infantry_Rifle") continue;
                rends[i].material.color = color;
            }
        }

        /// <summary>
        /// Flags the unit as actively capturing a building. Shows the Occupy
        /// animation (via <see cref="UnitAnimationDriver"/>) and reveals the flag.
        /// </summary>
        public void SetCapturing(bool capturing)
        {
            for (int i = 0; i < _animationDrivers.Length; i++)
                if (_animationDrivers[i] != null)
                    _animationDrivers[i].SetCapturing(capturing);
            for (int i = 0; i < _flagRoots.Length; i++)
                if (_flagRoots[i] != null)
                    _flagRoots[i].SetActive(capturing);
        }

        public void PlayAttackAnimation()
        {
            for (int i = 0; i < _animationDrivers.Length; i++)
                if (_animationDrivers[i] != null)
                    _animationDrivers[i].PlayAttack();
            for (int i = 0; i < _vehicleAnimationDrivers.Length; i++)
                if (_vehicleAnimationDrivers[i] != null)
                    _vehicleAnimationDrivers[i].PlayAttack();
        }

        /// <summary>Smoothly turns the model to face <paramref name="dir"/> (XZ only).</summary>
        public void FaceDirection(Vector3 dir)
        {
            if (ModelRoot == null) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            var target = FacingRotation(dir);
            ModelRoot.rotation = Quaternion.RotateTowards(
                ModelRoot.rotation, target, 540f * Time.deltaTime);
        }

        /// <summary>Immediately applies a final heading after movement completes.</summary>
        public void SetFacingDirection(Vector3 dir)
        {
            if (ModelRoot == null) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            ModelRoot.rotation = FacingRotation(dir);
        }

        Quaternion FacingRotation(Vector3 dir)
        {
            return Quaternion.LookRotation(dir.normalized, Vector3.up) *
                Quaternion.Euler(0f, _modelFacingYawOffset, 0f);
        }

        /// <summary>Smoothly turns the model toward a world-space point.</summary>
        public void FaceTowards(Vector3 worldPos)
        {
            FaceDirection(worldPos - transform.position);
        }


        void SetModelFlashColor(Color color)
        {
            if (_modelRenderers == null) return;
            for (int i = 0; i < _modelRenderers.Length; i++)
            {
                var renderer = _modelRenderers[i];
                if (renderer == null || renderer.name == "Infantry_Rifle") continue;
                renderer.material.color = color;
            }
        }
        /// <summary>Brief red flash used as "invalid target" feedback.</summary>
        public void FlashInvalid()
        {
            if (IsFlashing) return;
            IsFlashing = true;
            _flashTimer = 0.35f;
            SetModelFlashColor(new Color(1f, 0.25f, 0.25f));
        }

        /// <summary>Brief red flash shown when the unit takes a hit.</summary>
        public void FlashHit()
        {
            FlashInvalid();
        }

        /// <summary>Teardown when the unit dies: frees pooled UI and removes the view.</summary>
        public void Die()
        {
            ReleaseHealthBar();
            HideAttackCooldownBar();
            RemoveFromBattleServices();
            if (gameObject != null) Destroy(gameObject);
        }

        public void Extract()
        {
            if (Data == null || Data.ExitState != UnitExitState.Active) return;
            // Enter the extracting intermediate state first so the unit becomes
            // untargetable / immune to new damage while the suction animation plays.
            Data.ExitState = UnitExitState.Extracting;

            var move = UnitMovementController.Instance;
            if (move != null) move.CancelMove(this);
            var combat = UnitCombatController.Instance;
            if (combat != null) combat.CancelCombat(this);

            ReleaseHealthBar();
            HideAttackCooldownBar();
            var grid = BattleGridController.Instance;
            if (grid != null) grid.SetOccupied(Data.GridPosition, false);
            if (BattleSpatialIndex.Instance != null) BattleSpatialIndex.Instance.Unregister(this);
            if (UnitSelectionController.Instance != null)
                UnitSelectionController.Instance.Unregister(this);
            _removedFromBattle = true;

            StartCoroutine(SuctionAndExtract());
        }

        /// <summary>0.2s inhale / shrink into the portal, then finalises extraction (阶段 E).</summary>
        IEnumerator SuctionAndExtract()
        {
            const float duration = 0.2f;
            Vector3 startPos = transform.position;
            Vector3 startScale = ModelRoot != null ? ModelRoot.localScale : Vector3.one;
            Vector3 portalCenter = Vector3.zero;
            var extraction = ExtractionObjectiveController.Instance;
            if (extraction != null) portalCenter = extraction.PortalWorldCenter;

            SetModelFlashColor(new Color(0.4f, 0.85f, 1f));

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (ModelRoot != null)
                    ModelRoot.localScale = Vector3.Lerp(startScale, Vector3.zero, k);
                Vector3 target = Vector3.Lerp(startPos, portalCenter, k * 0.6f);
                target.y = startPos.y - k * 0.5f;
                transform.position = target;
                yield return null;
            }

            if (Data != null) Data.ExitState = UnitExitState.Extracted;
            if (CommanderGroupRegistry.Instance != null)
                CommanderGroupRegistry.Instance.NotifyUnitExtracted(this);
            if (gameObject != null) Destroy(gameObject);
        }

        void RemoveFromBattleServices()
        {
            if (_removedFromBattle) return;
            _removedFromBattle = true;
            if (BattleSpatialIndex.Instance != null) BattleSpatialIndex.Instance.Unregister(this);
            if (CommanderGroupRegistry.Instance != null)
                CommanderGroupRegistry.Instance.NotifyUnitRemoved(this);
            if (UnitSelectionController.Instance != null)
                UnitSelectionController.Instance.Unregister(this);
        }

        void LateUpdate()
        {
            RefreshModelSortingOrder();
        }

        void Update()
        {
            if (!IsFlashing) return;
            _flashTimer -= Time.deltaTime;
            if (_flashTimer > 0f) return;
            IsFlashing = false;
            TintModel(_normalTint);
        }
    }
}
