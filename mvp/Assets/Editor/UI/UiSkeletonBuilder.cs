using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;

namespace Mvp.EditorUI
{
    /// <summary>
    /// Milestone 1: builds the static UI skeleton for CommanderSelectScene and BattleScene.
    /// Menu: Tools/MVP/UI/...
    /// </summary>
    public static class UiSkeletonBuilder
    {
        const string SpriteDir = "Assets/Art/Battle/UI/sprites";
        const string UiArtDir = "Assets/Art/Battle/UI";
        const string BattleGeneratedDir = "Assets/Art/Battle/UI/Generated";
        const string CsUiArtDir = "Assets/Art/CommanderSelect/UI";
        const string CsGeneratedDir = "Assets/Art/CommanderSelect/UI/Generated";
        const string PrefabBattleDir = "Assets/Prefabs/Battle/UI";
        const string PrefabCsDir = "Assets/Prefabs/CommanderSelect/UI";

        const float RefW = 1600f;
        const float RefH = 900f;

        static readonly Color PanelTint = new Color(0.035f, 0.095f, 0.11f, 0.96f);
        static readonly Color GoldText = new Color(1f, 0.86f, 0.48f, 1f);

        [MenuItem("Tools/MVP/UI/Rebuild All UI")]
        public static void RebuildAllUi()
        {
            ConfigureSprites();
            CreateCjkFont();
            CreateBasePrefabs();
            BuildCommanderSelectSceneUI();
            BuildBattleSceneUI();
            Debug.Log("[UiSkeleton] Rebuilt all responsive UI scenes and prefabs.");
        }

        [MenuItem("Tools/MVP/UI/Rebuild Commander Select UI")]
        public static void RebuildCommanderSelectUi()
        {
            ConfigureSprites();
            CreateCjkFont();
            BuildCommanderSelectSceneUI();
            Debug.Log("[UiSkeleton] Rebuilt CommanderSelectScene UI only.");
        }

        // ------------------------------------------------------------------
        // Menu: configure sprite imports (texture type + 9-slice borders)
        // ------------------------------------------------------------------
        [MenuItem("Tools/MVP/UI/Configure Sprites")]
        public static void ConfigureSprites()
        {
            // UI component sprites
            string[] spriteFiles = Directory.GetFiles(GetFullPath(SpriteDir), "*.png");
            int n = 0;
            foreach (var f in spriteFiles)
            {
                string path = ToAssetPath(f);
                if (path == null) continue;
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool changed = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; changed = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (ti.filterMode != FilterMode.Bilinear) { ti.filterMode = FilterMode.Bilinear; changed = true; }
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                int border = PickBorder(Path.GetFileNameWithoutExtension(f));
                if (ti.spriteBorder != new Vector4(border, border, border, border)) { ti.spriteBorder = new Vector4(border, border, border, border); changed = true; }
                if (changed) { ti.SaveAndReimport(); }
                n++;
            }

            // Reference / background images -> sprites
            string[] refImages = new string[]
            {
                UiArtDir + "/battle_ui_demo.png",
                UiArtDir + "/battle_ui_components.png",
                UiArtDir + "/commander_detail.png",
                CsUiArtDir + "/background.png",
                CsUiArtDir + "/commander_select_ui_demo.png",
            };
            foreach (var p in refImages)
            {
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                bool changed = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; changed = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (ti.filterMode != FilterMode.Bilinear) { ti.filterMode = FilterMode.Bilinear; changed = true; }
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
                if (changed) { ti.SaveAndReimport(); }
            }

            // Dedicated Commander Select components generated from the approved visual reference.
            string[] generatedDirs = { CsGeneratedDir, BattleGeneratedDir };
            foreach (var generatedDir in generatedDirs)
            {
                if (!Directory.Exists(GetFullPath(generatedDir))) continue;
                foreach (var f in Directory.GetFiles(GetFullPath(generatedDir), "*.png", SearchOption.AllDirectories))
                {
                    if (Path.GetFileNameWithoutExtension(f).EndsWith("_chroma")) continue;
                    string path = ToAssetPath(f);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) continue;
                    bool changed = false;
                    if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; changed = true; }
                    if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (ti.filterMode != FilterMode.Bilinear) { ti.filterMode = FilterMode.Bilinear; changed = true; }
                    if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
                    if (ti.alphaSource != TextureImporterAlphaSource.FromInput) { ti.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
                    if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                    if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; changed = true; }
                    if (changed) ti.SaveAndReimport();
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[UiSkeleton] Configured {n} component sprites + {refImages.Length} reference sprites.");
        }

        static int PickBorder(string fileName)
        {
            // Large panels get a larger slice border; small panels smaller.
            if (fileName.Contains("_09_")) return 48; // 792x317
            if (fileName.Contains("_11_")) return 48; // 490x378
            if (fileName.Contains("_14_")) return 48; // 1031x232 wide
            if (fileName.Contains("_04_")) return 22; // gold button
            if (fileName.Contains("_15_")) return 16; // zoom button
            return 20;
        }

        // ------------------------------------------------------------------
        // Menu: create base UI prefabs
        // ------------------------------------------------------------------
        [MenuItem("Tools/MVP/UI/Create Base Prefabs")]
        public static void CreateBasePrefabs()
        {
            EnsureFolder(PrefabBattleDir);
            EnsureFolder(PrefabCsDir);

            // FramedPanel.prefab
            var framed = BuildFramedPanel();
            SavePrefab(framed, PrefabBattleDir + "/FramedPanel.prefab");

            // GoldButton.prefab
            var goldBtn = BuildGoldButton("GoldButton");
            SavePrefab(goldBtn, PrefabBattleDir + "/GoldButton.prefab");

            // CardSlot.prefab
            var card = BuildCardSlot();
            SavePrefab(card, PrefabBattleDir + "/CardSlot.prefab");

            // CommanderPanel.prefab
            var cmd = BuildCommanderPanel();
            SavePrefab(cmd, PrefabBattleDir + "/CommanderPanel.prefab");

            // MiniMapPanel.prefab
            var mini = BuildMiniMapPanel();
            SavePrefab(mini, PrefabBattleDir + "/MiniMapPanel.prefab");

            // TooltipPanel.prefab (simple)
            var tooltip = BuildTooltipPanel();
            SavePrefab(tooltip, PrefabBattleDir + "/TooltipPanel.prefab");

            Debug.Log("[UiSkeleton] Base prefabs created in " + PrefabBattleDir);
        }

        // ------------------------------------------------------------------
        // Menu: build battle scene UI
        // ------------------------------------------------------------------
        [MenuItem("Tools/MVP/UI/Build Battle Scene UI")]
        public static void BuildBattleSceneUI()
        {
            const string scenePath = "Assets/Scenes/BattleScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            CleanUiRoots();

            var canvas = EnsureCanvas();
            EnsureEventSystem();
            AddComponentFromScript(canvas.gameObject, "Assets/Scripts/Battle/UI/BattleUiController.cs");

            // Root
            var root = CreateStretch("BattleUI", canvas.transform);

            var topLeft = CreateRect("TopLeft", root, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(28, -24), new Vector2(330, 58));
            BuildTopLeft(topLeft);

            var topRight = CreateRect("TopRight", root, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-28, -24), new Vector2(276, 58));
            BuildTopRight(topRight);

            var cmdPanel = BuildCommanderPanel();
            SetRect(cmdPanel, root, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(18, 176), new Vector2(470, 178));

            var cardBar = CreateRect("CardBar", root, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(18, 34), new Vector2(620, 140));
            BuildCardBar(cardBar);

            var formation = CreateRect("FormationPanel", root, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(650, 20), new Vector2(116, 220));
            BuildFormationPanel(formation);

            var minimap = BuildMiniMapPanel();
            SetRect(minimap, root, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-24, 22), new Vector2(300, 232));

            var startBattle = BuildCommanderSelectButton("StartBattleBtn", "开始战斗",
                CsGeneratedDir + "/embark_button.png", 24);
            SetRect(startBattle, root, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -18), new Vector2(190, 52));

            var statusBand = CreateRect("StatusBand", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -76), new Vector2(500, 28));
            var statusBg = statusBand.gameObject.AddComponent<Image>();
            statusBg.color = Color.clear;
            statusBg.raycastTarget = false;
            var debugText = CreateStretchText("StatusText", statusBand, "", 14);
            debugText.color = new Color(1f, 0.88f, 0.58f, 0.95f);
            debugText.outlineColor = new Color32(20, 14, 8, 220);
            debugText.outlineWidth = 0.16f;
            debugText.gameObject.AddComponent<Mvp.Battle.BattleUiStatusText>();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[UiSkeleton] BattleScene UI built.");
        }

        [MenuItem("Tools/MVP/UI/Rebuild Battle UI")]
        public static void RebuildBattleUi()
        {
            ConfigureSprites();
            CreateCjkFont();
            BuildBattleSceneUI();
            Debug.Log("[UiSkeleton] Rebuilt BattleScene UI only.");
        }









        // ------------------------------------------------------------------
        // Menu: build commander select scene UI
        // ------------------------------------------------------------------
        [MenuItem("Tools/MVP/UI/Build Commander Select Scene UI")]
        public static void BuildCommanderSelectSceneUI()
        {
            const string scenePath = "Assets/Scenes/CommanderSelectScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            CleanUiRoots();

            var canvas = EnsureCanvas();
            EnsureEventSystem();
            AddComponentFromScript(canvas.gameObject,
                "Assets/Scripts/CommanderSelect/CommanderSelectController.cs");

            // Background full-screen
            var bg = CreateStretch("Background", canvas.transform);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = LoadSprite(CsUiArtDir + "/background.png");
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
            bgImg.raycastTarget = false;

            var root = CreateStretch("CommanderSelectUI", canvas.transform);

            var title = CreateText("Title", root, "选择指挥官", 40, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -50), new Vector2(520, 58));
            title.color = new Color(0.25f, 0.16f, 0.07f, 1f);

            var returnBtn = BuildCommanderSelectButton("ReturnBtn", "返回",
                CsGeneratedDir + "/return_button.png", 25);
            SetRect(returnBtn, root, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(60, -52), new Vector2(165, 52));

            var summaryCol = CreateRect("SummaryColumn", root, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(190, -145), new Vector2(310, 400));
            BuildSummaryColumn(summaryCol);

            var detail = CreateRectGo("FramedPanel", null, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(785, 410));
            var detailImage = detail.AddComponent<Image>();
            detailImage.sprite = LoadSprite(CsGeneratedDir + "/detail_placeholder_panel.png");
            detailImage.type = Image.Type.Simple;
            detailImage.raycastTarget = false;
            SetRect(detail, root, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(555, -135), new Vector2(785, 410));
            var detailTitle = CreateText("DetailTitle", detail.transform, "", 34,
                new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(570, 48));
            detailTitle.color = new Color(0.25f, 0.16f, 0.07f, 1f);
            var detailBody = CreateText("DetailBody", detail.transform, "", 23,
                new Vector2(0.5f, 0.5f), new Vector2(80, -24), new Vector2(480, 235),
                TextAlignmentOptions.Left);
            detailBody.color = new Color(0.22f, 0.14f, 0.06f, 1f);

            var cardRow = CreateRect("CommanderCardRow", root, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(120, 48), new Vector2(1220, 290));
            BuildCommanderCards(cardRow);

            var embark = BuildCommanderSelectButton("EmbarkBtn", "出征",
                CsGeneratedDir + "/embark_button.png", 31);
            SetRect(embark, root, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-60, 48), new Vector2(180, 66));

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[UiSkeleton] CommanderSelectScene UI built.");
        }

        // ------------------------------------------------------------------
        // Menu: create the CJK TMP font asset (dynamic atlas) used by all UI text
        // ------------------------------------------------------------------
        const string CjkFontDir = "Assets/Art/Battle/UI/Fonts";
        const string CjkFontSrc = "Assets/Art/Battle/UI/Fonts/SimHei.ttf";
        const string CjkFontAsset = "Assets/Art/Battle/UI/Fonts/SimHei SDF.asset";

        [InitializeOnLoadMethod]
        static void ScheduleCjkFontValidation()
        {
            EditorApplication.delayCall += CreateCjkFont;
        }

        [MenuItem("Tools/MVP/UI/Create CJK Font")]
        public static void CreateCjkFont()
        {
            EnsureFolder(CjkFontDir);

            // Note: IncludeFontData defaults to true for .ttf imports, so the
            // FontImporter settings are left at their defaults.
            var font = AssetDatabase.LoadAssetAtPath<Font>(CjkFontSrc);
            if (font == null)
            {
                Debug.LogError("[UiSkeleton] Cannot load SimHei font at " + CjkFontSrc);
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CjkFontAsset);
            if (IsCjkFontUsable(existing))
            {
                RefreshCjkFontUsers(existing);
                return;
            }

            // Dynamic population mode so CJK glyphs are baked on demand at runtime.
            var generated = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (generated == null) { Debug.LogError("[UiSkeleton] Failed to create CJK font asset"); return; }
            generated.name = "SimHei SDF";

            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, CjkFontAsset);
                existing = generated;
            }
            else
            {
                // Repair the main asset in place so its GUID and all UI references remain valid.
                if (generated.atlasTextures != null)
                {
                    foreach (var texture in generated.atlasTextures)
                        if (texture != null) AssetDatabase.AddObjectToAsset(texture, existing);
                }
                if (generated.material != null) AssetDatabase.AddObjectToAsset(generated.material, existing);

                EditorUtility.CopySerialized(generated, existing);
                existing.name = "SimHei SDF";
                Object.DestroyImmediate(generated);
            }

            if (existing.atlasTextures != null)
            {
                foreach (var texture in existing.atlasTextures)
                    if (texture != null && !AssetDatabase.Contains(texture))
                        AssetDatabase.AddObjectToAsset(texture, existing);
            }
            if (existing.material != null && !AssetDatabase.Contains(existing.material))
                AssetDatabase.AddObjectToAsset(existing.material, existing);

            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CjkFontAsset,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CjkFontAsset);
            RefreshCjkFontUsers(existing);
            Debug.Log("[UiSkeleton] Created or repaired CJK font asset: " + CjkFontAsset);
        }

        static bool IsCjkFontUsable(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || fontAsset.material == null || fontAsset.atlasTextures == null ||
                fontAsset.atlasTextures.Length == 0)
                return false;

            foreach (var texture in fontAsset.atlasTextures)
                if (texture == null) return false;

            return true;
        }

        static void RefreshCjkFontUsers(TMP_FontAsset fontAsset)
        {
            if (!IsCjkFontUsable(fontAsset)) return;

            var fallbackFonts = TMP_Settings.fallbackFontAssets;
            if (fallbackFonts != null && !fallbackFonts.Contains(fontAsset))
            {
                fallbackFonts.Add(fontAsset);
                EditorUtility.SetDirty(TMP_Settings.instance);
                AssetDatabase.SaveAssets();
            }

            fontAsset.ReadFontAssetDefinition();
            foreach (var label in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
            {
                if (label.font != fontAsset) continue;
                label.font = null;
                label.font = fontAsset;
                label.SetAllDirty();
            }
        }

        static TMP_FontAsset GetCjkFont()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CjkFontAsset);
        }

        // ------------------------------------------------------------------
        // Menu: toggle the reference-image overlay in the current scene
        // ------------------------------------------------------------------
        [MenuItem("Tools/MVP/UI/Toggle Reference Overlay")]
        public static void ToggleReferenceOverlay()
        {
            var overlays = Object.FindObjectsOfType<Image>(true);
            int toggled = 0;
            foreach (var img in overlays)
            {
                if (img.gameObject.name != "ReferenceOverlay") continue;
                bool next = !img.gameObject.activeSelf;
                img.gameObject.SetActive(next);
                toggled++;
                Debug.Log($"[UiSkeleton] ReferenceOverlay -> {next}");
            }
            if (toggled == 0) Debug.Log("[UiSkeleton] No ReferenceOverlay found in current scene.");
        }

        // ==================================================================
        // Builders
        // ==================================================================

        static GameObject BuildFramedPanel()
        {
            var go = CreateRectGo("FramedPanel", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 300));
            var img = go.AddComponent<Image>();
            img.color = PanelTint;
            AddGoldOutline(go, 2f);

            // Corner accents
            var cornerSpr = LoadSprite(SpriteDir, "_19_");
            AddCorner(go.transform, cornerSpr, new Vector2(0, 1), new Vector2(0, 0));
            AddCorner(go.transform, cornerSpr, new Vector2(1, 1), new Vector2(0, 0));
            AddCorner(go.transform, cornerSpr, new Vector2(0, 0), new Vector2(0, 0));
            AddCorner(go.transform, cornerSpr, new Vector2(1, 0), new Vector2(0, 0));
            return go;
        }

        static GameObject BuildGoldButton(string name = "GoldButton", string label = "按钮")
        {
            var go = CreateRectGo(name, null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 58));
            var img = go.AddComponent<Image>();
            img.color = new Color(0.035f, 0.13f, 0.16f, 0.98f);
            AddGoldOutline(go, 2f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.68f, 1f);
            colors.pressedColor = new Color(0.78f, 0.65f, 0.38f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var labelRt = CreateRect("Label", go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 40));
            var text = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22;
            text.alignment = TextAlignmentOptions.Center;
            text.color = GoldText;
            text.raycastTarget = false;
            var cjk = GetCjkFont();
            if (cjk != null) text.font = cjk;
            return go;
        }

        static GameObject BuildCardSlot()
        {
            var go = CreateRectGo("CardSlot", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92, 150));
            var img = go.AddComponent<Image>();
            img.color = new Color(0.025f, 0.09f, 0.11f, 0.98f);
            AddGoldOutline(go, 1.5f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;

            var badge = CreateRect("Badge", go.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(6, -6), new Vector2(26, 26));
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.color = new Color(0.72f, 0.48f, 0.18f, 1f);
            badge.localEulerAngles = new Vector3(0, 0, 45);

            var count = CreateText("Count", go.transform, "x0", 20, new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(90, 30));
            count.color = new Color(0.9f, 0.85f, 0.7f);
            return go;
        }

        static GameObject BuildCommanderSelectButton(string name, string label, string spritePath, int fontSize)
        {
            var go = CreateRectGo(name, null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 56));
            var img = go.AddComponent<Image>();
            img.sprite = LoadSprite(spritePath);
            img.type = Image.Type.Simple;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.72f, 1f);
            colors.pressedColor = new Color(0.78f, 0.68f, 0.48f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.56f, 0.56f, 0.56f, 0.62f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = CreateStretchText("Label", go.transform, label, fontSize);
            text.color = new Color(1f, 0.86f, 0.38f, 1f);
            if (name == "ReturnBtn")
            {
                var rt = (RectTransform)text.transform;
                rt.offsetMin = new Vector2(55, 3);
                rt.offsetMax = new Vector2(-12, -3);
            }
            return go;
        }

        static GameObject BuildCommanderPanel()
        {
            var go = CreateRectGo("CommanderPanel", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(470, 178));
            var panelSprite = LoadSprite(BattleGeneratedDir + "/commander_panel_blank.png");
            var img = go.AddComponent<RawImage>();
            img.texture = panelSprite != null ? panelSprite.texture : null;
            img.uvRect = new Rect(0, 0.074f, 1, 0.926f);
            img.raycastTarget = false;

            var portrait = CreateRect("Portrait", go.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 40), new Vector2(178, 202));
            var portraitImage = portrait.gameObject.AddComponent<Image>();
            portraitImage.sprite = LoadSprite(BattleGeneratedDir + "/commander_portrait.png");
            portraitImage.preserveAspect = true;

            var nameTxt = CreateText("Name", go.transform, "伊莲娜", 21,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, -20), new Vector2(182, 27),
                TextAlignmentOptions.Left);
            nameTxt.color = GoldText;

            var laurel = CreateRect("Laurel", go.transform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-16, -10), new Vector2(52, 48));
            var laurelImage = laurel.gameObject.AddComponent<Image>();
            laurelImage.sprite = LoadSprite(BattleGeneratedDir + "/commander_laurel.png");
            laurelImage.preserveAspect = true;
            laurelImage.raycastTarget = false;

            var heart = CreateRect("HealthHeart", go.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(180, -51), new Vector2(22, 22));
            var heartImage = heart.gameObject.AddComponent<Image>();
            heartImage.sprite = LoadSprite(BattleGeneratedDir + "/health_heart.png");
            heartImage.preserveAspect = true;
            heartImage.raycastTarget = false;

            var healthLabel = CreateText("HealthLabel", go.transform, "生命", 16,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(206, -51), new Vector2(52, 22),
                TextAlignmentOptions.Left);
            healthLabel.color = new Color(1f, 0.86f, 0.52f, 1f);

            var hpBg = CreateRect("HealthBarBg", go.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(180, -72), new Vector2(251, 15));
            var hpBgImg = hpBg.gameObject.AddComponent<Image>();
            hpBgImg.sprite = LoadSprite(BattleGeneratedDir + "/health_bar_bg.png");
            hpBgImg.type = Image.Type.Simple;
            hpBgImg.raycastTarget = false;

            var hpFill = CreateRect("HealthBarFill", hpBg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                Vector2.zero, new Vector2(243, 14));
            hpFill.pivot = new Vector2(0, 0.5f);
            var hpFillImg = hpFill.gameObject.AddComponent<Image>();
            hpFillImg.sprite = LoadSprite(BattleGeneratedDir + "/health_bar_fill.png");
            hpFillImg.type = Image.Type.Simple;
            hpFillImg.raycastTarget = false;

            var hpText = CreateText("HealthText", go.transform, "86/100", 16,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(278, -51), new Vector2(92, 22));
            hpText.color = new Color(1f, 0.9f, 0.62f, 1f);

            for (int i = 0; i < 4; i++)
            {
                float x = 177 + i * 66;
                var slot = CreateRect("Trait" + (i + 1), go.transform, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(x, -84), new Vector2(60, 72));
                var medallion = CreateRect("Medallion", slot, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(0, -1), new Vector2(48, 48));
                var badgeImage = medallion.gameObject.AddComponent<Image>();
                badgeImage.sprite = LoadSprite(BattleGeneratedDir + "/Composed/trait_badge_" + (i + 1) + ".png");
                badgeImage.preserveAspect = true;
                badgeImage.raycastTarget = false;
                var label = CreateText("TraitLabel", slot, "", 13, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(0, -52), new Vector2(64, 18));
                label.color = new Color(1f, 0.85f, 0.5f, 1f);
            }
            return go;
        }

        static GameObject BuildMiniMapPanel()
        {
            var go = CreateRectGo("MiniMapPanel", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 232));
            var mapArea = CreateRect("MapArea", go.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 0), new Vector2(300, 190));
            var mapImg = mapArea.gameObject.AddComponent<Image>();
            mapImg.sprite = LoadSprite(SpriteDir, "_11_");
            mapImg.type = Image.Type.Simple;
            mapImg.preserveAspect = false;

            var zoomBtn = CreateRect("ZoomBtn", go.transform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(0, -2), new Vector2(94, 40));
            var zoomImg = zoomBtn.gameObject.AddComponent<Image>();
            zoomImg.sprite = LoadSprite(SpriteDir, "_15_");
            zoomImg.type = Image.Type.Simple;
            return go;
        }

        static GameObject BuildTooltipPanel()
        {
            var go = CreateRectGo("TooltipPanel", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 120));
            var img = go.AddComponent<Image>();
            img.color = PanelTint;
            AddGoldOutline(go, 1.5f);

            var text = CreateText("TooltipText", go.transform, "", 20, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 100), TextAlignmentOptions.Left);
            text.color = new Color(0.95f, 0.9f, 0.8f);
            return go;
        }

        static RectTransform BuildCommanderPortrait(Transform parent, Vector2 pos, Vector2 size)
        {
            var viewport = CreateRect("Portrait", parent, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                pos, size);
            var background = viewport.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.08f, 0.09f, 1f);
            AddGoldOutline(viewport.gameObject, 1.5f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var art = CreateStretch("Art", viewport);
            art.offsetMin = new Vector2(2, 2);
            art.offsetMax = new Vector2(-2, -2);
            var portrait = art.gameObject.AddComponent<RawImage>();
            var source = LoadSprite(SpriteDir, "_09_");
            portrait.texture = source != null ? source.texture : null;
            portrait.uvRect = new Rect(0f, 0f, 0.41f, 1f);
            portrait.raycastTarget = false;
            return viewport;
        }

        // ------------------------------------------------------------------
        // Scene sub-builders
        // ------------------------------------------------------------------

        static void BuildTopLeft(Transform parent)
        {
            var settings = CreateRect("SettingsBtn", parent, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                Vector2.zero, new Vector2(112, 51));
            var settingsImg = settings.gameObject.AddComponent<Image>();
            settingsImg.sprite = LoadSprite(SpriteDir, "_01_");
            settingsImg.type = Image.Type.Simple;
            var settingsButton = settings.gameObject.AddComponent<Button>();
            settingsButton.targetGraphic = settingsImg;

            var coinIcon = CreateRect("CoinIcon", parent, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(132, 0), new Vector2(70, 48));
            var coinIconImg = coinIcon.gameObject.AddComponent<Image>();
            coinIconImg.sprite = LoadSprite(SpriteDir, "_04_");
            coinIconImg.preserveAspect = true;
            coinIconImg.raycastTarget = false;
            var coinsText = CreateText("CoinsText", parent, "金币  30", 19,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(204, 0), new Vector2(118, 36),
                TextAlignmentOptions.Left);
            coinsText.color = new Color(1f, 0.86f, 0.5f, 1f);
        }

        static void BuildTopRight(Transform parent)
        {
            string[] prefixes = { "_02_", "_03_" };
            string[] names = { "TroopsBtn", "CardsBtn" };
            for (int i = 0; i < 2; i++)
            {
                var buttonRt = CreateRect(names[i], parent, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(-(1 - i) * 138, 0), new Vector2(126, 50));
                var image = buttonRt.gameObject.AddComponent<Image>();
                image.sprite = LoadSprite(SpriteDir, prefixes[i]);
                image.type = Image.Type.Simple;
                var button = buttonRt.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
            }
        }

        static void BuildCardBar(Transform parent)
        {
            var cardStrip = LoadSprite(SpriteDir, "_14_");

            for (int i = 0; i < 6; i++)
            {
                var slot = CreateRect("CardSlot" + (i + 1), parent, new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(i * 103.5f, 4), new Vector2(100, 132));
                var slotImage = slot.gameObject.AddComponent<RawImage>();
                slotImage.texture = cardStrip != null ? cardStrip.texture : null;
                slotImage.uvRect = new Rect(1f / 6f, 0.052f, 1f / 6f, 0.896f);
                slotImage.color = Color.white;
                slotImage.raycastTarget = true;
                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slotImage;

                var badge = CreateRect("Badge", slot, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(7, -7), new Vector2(25, 25));
                var badgeImage = badge.gameObject.AddComponent<Image>();
                badgeImage.sprite = LoadSprite(BattleGeneratedDir + "/unit_card_badge.png");
                badgeImage.preserveAspect = true;
                badgeImage.enabled = false;

                var nameTxt = CreateText("Name", slot, "待解锁", 13,
                    new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(8, -11), new Vector2(72, 20));
                nameTxt.color = new Color(0.95f, 0.88f, 0.72f, 0.9f);

                var countPatch = CreateRect("CountPatch", slot, new Vector2(0.5f, 0),
                    new Vector2(0, 14), new Vector2(72, 22));
                var patchImage = countPatch.gameObject.AddComponent<Image>();
                patchImage.color = new Color(0.065f, 0.13f, 0.14f, 1f);
                patchImage.raycastTarget = false;
                var count = CreateText("Count", slot, "×1", 16,
                    new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(72, 24));
                count.color = new Color(1f, 0.86f, 0.55f, 1f);
            }
        }

        static void BuildFormationPanel(Transform parent)
        {
            var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            string[] labels = { "竖向", "横向", "方形" };
            string[] prefixes = { "_10_", "_12_", "_13_" };
            for (int i = 0; i < 3; i++)
            {
                var buttonGo = CreateRectGo("FormationBtn" + (i + 1), null, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(116, 68));
                var image = buttonGo.AddComponent<Image>();
                image.sprite = LoadSprite(SpriteDir, prefixes[i]);
                image.type = Image.Type.Simple;
                var button = buttonGo.AddComponent<Button>();
                button.targetGraphic = image;
                buttonGo.transform.SetParent(parent, false);
                var element = buttonGo.AddComponent<LayoutElement>();
                element.preferredHeight = 68;
                var hiddenLabel = CreateStretchText("Label", buttonGo.transform, labels[i], 1);
                hiddenLabel.color = Color.clear;
            }
        }

        static void BuildSummaryColumn(Transform parent)
        {
            var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            string[] titles = { "指挥官", "特性", "初始兵种" };
            string[] bodies = { "未选择", "未选择特性", "未选择兵种" };
            for (int i = 0; i < 3; i++)
            {
                var panel = CreateRectGo("SummaryPanel" + (i + 1), null, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(310, 120));
                var image = panel.AddComponent<Image>();
                image.sprite = LoadSprite(CsGeneratedDir + "/summary_panel.png");
                image.type = Image.Type.Simple;
                image.raycastTarget = false;
                panel.transform.SetParent(parent, false);
                var element = panel.AddComponent<LayoutElement>();
                element.preferredHeight = 120;
                var t = CreateText("Title", panel.transform, titles[i], 21,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -8), new Vector2(274, 34),
                    TextAlignmentOptions.Left);
                t.color = new Color(1f, 0.82f, 0.34f, 1f);
                var body = CreateText("Body", panel.transform, bodies[i], 19,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -51), new Vector2(262, 57),
                    TextAlignmentOptions.Left);
                body.color = new Color(0.21f, 0.14f, 0.07f, 1f);
            }
        }

        static void BuildCommanderCards(Transform parent)
        {
            var layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 18;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            string[] names = { "伊莲娜", "待解锁", "待解锁", "待解锁", "待解锁", "待解锁" };
            string[] emblems = { "盾", "桂", "阳", "骑", "冠", "月" };
            for (int i = 0; i < 6; i++)
            {
                var slot = CreateRectGo("CommanderCard" + (i + 1), null, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(170, 282));
                var cardImage = slot.AddComponent<Image>();
                cardImage.sprite = LoadSprite(CsGeneratedDir + "/commander_card_frame.png");
                cardImage.type = Image.Type.Simple;
                var button = slot.AddComponent<Button>();
                button.targetGraphic = cardImage;
                slot.transform.SetParent(parent, false);
                var slotRt = (RectTransform)slot.transform;
                slotRt.sizeDelta = new Vector2(170, 282);

                var portrait = CreateText("Portrait", slot.transform, "像", 58,
                    new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(130, 112));
                portrait.color = new Color(0.46f, 0.32f, 0.14f, 0.2f);
                portrait.overflowMode = TextOverflowModes.Overflow;

                var t = CreateText("Name", slot.transform, names[i], 18,
                    new Vector2(0.5f, 0), new Vector2(0, 75), new Vector2(146, 34));
                t.color = new Color(1f, 0.84f, 0.39f, 1f);
                var count = CreateText("Count", slot.transform, i == 0 ? "1" : "—", 15,
                    new Vector2(0.5f, 0), new Vector2(0, 47), new Vector2(130, 25));
                count.color = new Color(0.83f, 0.76f, 0.58f, 1f);
                var emblem = CreateText("Emblem", slot.transform, emblems[i], 29,
                    new Vector2(0.5f, 0), new Vector2(0, 19), new Vector2(60, 42));
                emblem.color = new Color(1f, 0.77f, 0.24f, 1f);
            }
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        static GameObject BuildReferenceOverlay(Transform parent, string spritePath)
        {
            var overlay = CreateStretch("ReferenceOverlay", parent);
            var img = overlay.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(spritePath);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
            var color = Color.white;
            color.a = 0.4f;
            img.color = color;
            return overlay.gameObject;
        }

        static void CleanUiRoots()
        {
            var existing = Object.FindObjectsOfType<Canvas>();
            foreach (var c in existing)
            {
                if (c.transform.parent == null)
                {
                    Object.DestroyImmediate(c.gameObject);
                }
            }
            var es = Object.FindObjectOfType<EventSystem>();
            if (es != null) Object.DestroyImmediate(es.gameObject);
        }

        static Canvas EnsureCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        static EventSystem EnsureEventSystem()
        {
            var es = Object.FindObjectOfType<EventSystem>();
            if (es != null) return es;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return go.GetComponent<EventSystem>();
        }

        static GameObject CreateRectGo(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            SetAnchor(rt, anchor);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = CreateRectGo(name, parent, anchor, pos, size);
            return (RectTransform)go.transform;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size)
        {
            var rt = CreateRect(name, parent, anchor, pos, size);
            rt.pivot = pivot;
            return rt;
        }

        static RectTransform CreateStretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void SetRect(GameObject go, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            SetAnchor(rt, anchor);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void SetRect(GameObject go, Transform parent, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size)
        {
            SetRect(go, parent, anchor, pos, size);
            ((RectTransform)go.transform).pivot = pivot;
        }

        static void SetAnchor(RectTransform rt, Vector2 anchor)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
        }

        static TextMeshProUGUI CreateText(string name, Transform parent, string text, int size,
            Vector2 anchor, Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = CreateRect(name, parent, anchor, pos, sizeDelta);
            return ConfigureText(rt, text, size, align);
        }

        static TextMeshProUGUI CreateText(string name, Transform parent, string text, int size,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sizeDelta,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = CreateRect(name, parent, anchor, pivot, pos, sizeDelta);
            return ConfigureText(rt, text, size, align);
        }

        static TextMeshProUGUI CreateStretchText(string name, Transform parent, string text, int size,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = CreateStretch(name, parent);
            rt.offsetMin = new Vector2(14, 3);
            rt.offsetMax = new Vector2(-14, -3);
            return ConfigureText(rt, text, size, align);
        }

        static TextMeshProUGUI ConfigureText(RectTransform rt, string text, int size,
            TextAlignmentOptions align)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = GoldText;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            var cjk = GetCjkFont();
            if (cjk != null) t.font = cjk;
            return t;
        }

        static void AddCorner(Transform parent, Sprite spr, Vector2 anchor, Vector2 offset)
        {
            var rt = CreateRect("Corner", parent, anchor, anchor, offset, new Vector2(32, 32));
            rt.localScale = new Vector3(anchor.x > 0.5f ? -1 : 1, anchor.y < 0.5f ? -1 : 1, 1);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = spr;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
        }

        static void AddGoldOutline(GameObject target, float thickness)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.57f, 0.25f, 0.95f);
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = true;
        }

        static Sprite LoadSprite(string spriteDir, string prefix)
        {
            string[] guids = AssetDatabase.FindAssets("", new[] { spriteDir });
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                string fn = Path.GetFileName(p);
                if (fn.StartsWith("comp") && fn.Contains(prefix))
                    return AssetDatabase.LoadAssetAtPath<Sprite>(p);
            }
            return null;
        }

        static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string leaf = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        static void SavePrefab(GameObject go, string path)
        {
            if (go == null) return;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static void AddComponentFromScript(GameObject target, string scriptPath)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            var componentType = script != null ? script.GetClass() : null;
            if (componentType == null)
            {
                Debug.LogError("[UiSkeleton] Cannot resolve component script: " + scriptPath);
                return;
            }

            target.AddComponent(componentType);
        }

        static string GetFullPath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath).Replace('/', '\\');
        }

        static string ToAssetPath(string fullPath)
        {
            string root = Directory.GetCurrentDirectory().Replace('\\', '/') + "/";
            fullPath = fullPath.Replace('\\', '/');
            if (fullPath.StartsWith(root)) return fullPath.Substring(root.Length);
            return null;
        }
    }
}
