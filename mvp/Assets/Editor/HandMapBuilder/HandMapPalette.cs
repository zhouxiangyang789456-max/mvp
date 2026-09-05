using System.Collections.Generic;

namespace Mvp.Editor.HandMapBuilder
{
    /// <summary>
    /// Static category → prefab-path table for the Hand Map Builder palette
    /// (HandMapBuilder阶段2分类与层级改造方案 §2).
    ///
    /// Each HandMapCategory maps to a list of asset paths under
    /// "Assets/Isometric Pack 3d/...". The HandMapBuilderWindow reads this
    /// dictionary to populate its category tabs and pagination.
    ///
    /// Adding new prefabs: append to the matching list. The category ordering
    /// here drives tab ordering in the UI.
    /// </summary>
    public enum HandMapCategory
    {
        Base,
        Path,
        Forest,
        Plant,
        Water,
        Ramp,
        Bridge,
        Mountain,
        Building,
        Decoration,
        Effect,
        Erase
    }

    public static class HandMapPalette
    {
        public const int MaxZ = 10; // 层级上限：第 1~10 层（Z=0..9）

        public static readonly Dictionary<HandMapCategory, List<string>> Paths
            = new Dictionary<HandMapCategory, List<string>>
        {
            // ── 基础地形：草地 + 边角件 + 砖块 ──
            [HandMapCategory.Base] = new List<string>
            {
                "Assets/Isometric Pack 3d/Props/Tile_Grass1",
                "Assets/Isometric Pack 3d/Props/Tile_Grass2",
                "Assets/Isometric Pack 3d/Props/Tile_Grass3",
                "Assets/Isometric Pack 3d/Props/Tile_Grass4",
                "Assets/Isometric Pack 3d/Props/Tile_Grass5",
                "Assets/Isometric Pack 3d/Props/Tile1_Base",
                "Assets/Isometric Pack 3d/Props/Tile_1_A",
                "Assets/Isometric Pack 3d/Props/Tile_1_B",
                "Assets/Isometric Pack 3d/Props/Tile_1_C",
                "Assets/Isometric Pack 3d/Props/Tile_1_D",
                "Assets/Isometric Pack 3d/Props/Tile_1_E",
                "Assets/Isometric Pack 3d/Props/Tile_1_F",
                "Assets/Isometric Pack 3d/Props/Tile_1_G",
                "Assets/Isometric Pack 3d/Props/Tile_1_H",
                "Assets/Isometric Pack 3d/Props/Tile_1_brick_A",
                "Assets/Isometric Pack 3d/Props/Tile_1_brick_B",
                "Assets/Isometric Pack 3d/Props/Tile_1_brick_C",
                "Assets/Isometric Pack 3d/Props/Tile_1_brick_D",
                "Assets/Isometric Pack 3d/Props/Tile_1_brick_E",
                "Assets/Isometric Pack 3d/Props/Tile_1_brick_F",
                "Assets/Isometric Pack 3d/Props/Tile_1_brick_G",
                "Assets/Isometric Pack 3d/Props/Tile_1_brick_H",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base1A",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base1B",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base1C",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base1D",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base1E",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base1F",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base1G",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base1H",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base2",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base3",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Base4",
            },

            // ── 道路砖（拼接组）──
            [HandMapCategory.Path] = new List<string>
            {
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group1",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group1B",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group2",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group2B",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group3",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group3B",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group4",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group4B",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group5",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group5B",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group6",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group7",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group8",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group9",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group10",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group11",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group12",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group13",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group14",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group15",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group16",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group17",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Group18",
            },

            // ── 森林 / 树 ──
            [HandMapCategory.Forest] = new List<string>
            {
                "Assets/Isometric Pack 3d/Props/Tree1_1",
                "Assets/Isometric Pack 3d/Props/Tree1_1b",
                "Assets/Isometric Pack 3d/Props/Tree1_2",
                "Assets/Isometric Pack 3d/Props/Tree1_2b",
                "Assets/Isometric Pack 3d/Props/Tree1_3",
                "Assets/Isometric Pack 3d/Props/Tree1_3b",
                "Assets/Isometric Pack 3d/Props/Tree1_4",
                "Assets/Isometric Pack 3d/Props/Tree1_4b",
                "Assets/Isometric Pack 3d/Props/Tree1_5",
                "Assets/Isometric Pack 3d/Props/Tree1_5b",
                "Assets/Isometric Pack 3d/Props/Tree2_1",
                "Assets/Isometric Pack 3d/Props/Tree2_2",
                "Assets/Isometric Pack 3d/Props/Tree2_3",
                "Assets/Isometric Pack 3d/Props/Tree2_4",
                "Assets/Isometric Pack 3d/Props/Tree2_5",
                "Assets/Isometric Pack 3d/Props/Tree2_6",
                "Assets/Isometric Pack 3d/Props/Tree2_7",
                "Assets/Isometric Pack 3d/Props/Tree2_8",
                "Assets/Isometric Pack 3d/Props/Tree3_01",
                "Assets/Isometric Pack 3d/Props/Tree3_01_aut",
                "Assets/Isometric Pack 3d/Props/Tree3_02",
                "Assets/Isometric Pack 3d/Props/Tree3_02_aut",
                "Assets/Isometric Pack 3d/Props/Tree3_03",
                "Assets/Isometric Pack 3d/Props/Tree3_03_aut",
                "Assets/Isometric Pack 3d/Props/Tree3_04",
                "Assets/Isometric Pack 3d/Props/Tree3_04_aut",
                "Assets/Isometric Pack 3d/Props/Tree3_05",
                "Assets/Isometric Pack 3d/Props/Tree3_05_aut",
                "Assets/Isometric Pack 3d/Props/Tree3_06",
                "Assets/Isometric Pack 3d/Props/Tree3_06_aut",
                "Assets/Isometric Pack 3d/Props/Tree3_07",
                "Assets/Isometric Pack 3d/Props/Tree3_07_aut",
                "Assets/Isometric Pack 3d/Props/Tree4_01",
                "Assets/Isometric Pack 3d/Props/Tree4_02",
                "Assets/Isometric Pack 3d/Props/Tree4_03",
                "Assets/Isometric Pack 3d/Props/Tree4_04",
                "Assets/Isometric Pack 3d/Props/Tree4_05",
            },

            // ── 灌木 / 草 ──
            [HandMapCategory.Plant] = new List<string>
            {
                "Assets/Isometric Pack 3d/Props/Plants_01A",
                "Assets/Isometric Pack 3d/Props/Plants_01B",
                "Assets/Isometric Pack 3d/Props/Plants_02A",
                "Assets/Isometric Pack 3d/Props/Plants_02B",
                "Assets/Isometric Pack 3d/Props/Plants_02C",
                "Assets/Isometric Pack 3d/Props/Plants_03A",
                "Assets/Isometric Pack 3d/Props/Plants_03B",
                "Assets/Isometric Pack 3d/Props/Plants_03C",
                "Assets/Isometric Pack 3d/Props/Plants_03D",
                "Assets/Isometric Pack 3d/Props/Plants_04A",
                "Assets/Isometric Pack 3d/Props/Plants_04B",
                "Assets/Isometric Pack 3d/Props/Plants_05",
                "Assets/Isometric Pack 3d/Props/Plants_06",
                "Assets/Isometric Pack 3d/Props/Plants_07",
                "Assets/Isometric Pack 3d/Props/Plants_08",
                "Assets/Isometric Pack 3d/Props/Plants_09",
                "Assets/Isometric Pack 3d/Props/Plants_10",
                "Assets/Isometric Pack 3d/Props/Plants_11",
                "Assets/Isometric Pack 3d/Props/Plants_12",
                "Assets/Isometric Pack 3d/Props/Plants_13",
                "Assets/Isometric Pack 3d/Props/Plants_14",
                "Assets/Isometric Pack 3d/Props/Plants_15",
                "Assets/Isometric Pack 3d/Props/Plants_16",
                "Assets/Isometric Pack 3d/Props/Plants_17",
                "Assets/Isometric Pack 3d/Props/Plants_17B",
                "Assets/Isometric Pack 3d/Props/Plants_18",
                "Assets/Isometric Pack 3d/Props/Plants_19",
                "Assets/Isometric Pack 3d/Props/Plants_20",
                "Assets/Isometric Pack 3d/Props/Plants_21",
                "Assets/Isometric Pack 3d/Props/Plants_22A",
                "Assets/Isometric Pack 3d/Props/Plants_22B",
                "Assets/Isometric Pack 3d/Props/Plants_22C",
                "Assets/Isometric Pack 3d/Props/Plants_22D",
                "Assets/Isometric Pack 3d/Props/Plants_23",
                "Assets/Isometric Pack 3d/Props/Plants_24",
                "Assets/Isometric Pack 3d/Props/Ground_leafs_1",
                "Assets/Isometric Pack 3d/Props/Ground_leafs_2",
                "Assets/Isometric Pack 3d/Props/Mushroom1A",
                "Assets/Isometric Pack 3d/Props/Mushroom1B",
                "Assets/Isometric Pack 3d/Props/Mushroom2A",
                "Assets/Isometric Pack 3d/Props/Mushroom2B",
                "Assets/Isometric Pack 3d/Props/Mushroom3A",
                "Assets/Isometric Pack 3d/Props/Mushroom3B",
                "Assets/Isometric Pack 3d/Props/Mushroom4A",
                "Assets/Isometric Pack 3d/Props/Mushroom4B",
            },

            // ── 水 ──
            [HandMapCategory.Water] = new List<string>
            {
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Water1A",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Water1B",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Water1C",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Water1D",
                "Assets/Isometric Pack 3d/Tiles_Groups/Tile_Water2",
                "Assets/Isometric Pack 3d/Props/Water1",
            },

            // ── 坡道 / 楼梯 ──
            [HandMapCategory.Ramp] = new List<string>
            {
                "Assets/Isometric Pack 3d/Props/Tile_Ramp1A",
                "Assets/Isometric Pack 3d/Props/Tile_Ramp1B",
                "Assets/Isometric Pack 3d/Props/Tile_Ramp2A",
                "Assets/Isometric Pack 3d/Props/Tile_Ramp2B",
                "Assets/Isometric Pack 3d/Props/Stairs_1",
                "Assets/Isometric Pack 3d/Props/Stairs_2",
                "Assets/Isometric Pack 3d/Props/Stairs_Debris1",
                "Assets/Isometric Pack 3d/Props/Stairs_Debris2",
                "Assets/Isometric Pack 3d/Props/Stairs_Debris3",
            },

            // ── 桥 ──
            [HandMapCategory.Bridge] = new List<string>
            {
                "Assets/Isometric Pack 3d/Props/Bridge1",
                "Assets/Isometric Pack 3d/Props/Bridge2",
            },

            // ── 山地 / 岩石 ──
            [HandMapCategory.Mountain] = new List<string>
            {
                "Assets/Isometric Pack 3d/Props/Rock_01",
                "Assets/Isometric Pack 3d/Props/Rock_02",
                "Assets/Isometric Pack 3d/Props/Rock_03",
                "Assets/Isometric Pack 3d/Props/Rock_04",
                "Assets/Isometric Pack 3d/Props/Rock_05",
                "Assets/Isometric Pack 3d/Props/Rock_06",
                "Assets/Isometric Pack 3d/Props/Rock_07",
                "Assets/Isometric Pack 3d/Props/Rock_08",
                "Assets/Isometric Pack 3d/Props/Rock_09",
                "Assets/Isometric Pack 3d/Props/Rock_10",
                "Assets/Isometric Pack 3d/Props/Rock_11",
            },

            // ── 建筑 / 营地 ──
            [HandMapCategory.Building] = new List<string>
            {
                "Assets/Isometric Pack 3d/Props/Camp1_Brick1",
                "Assets/Isometric Pack 3d/Props/Camp1_Brick2",
                "Assets/Isometric Pack 3d/Props/Camp1_Brick3",
                "Assets/Isometric Pack 3d/Props/Camp1_Brick4",
                "Assets/Isometric Pack 3d/Props/Camp1_Barricade1",
                "Assets/Isometric Pack 3d/Props/Camp1_Barricade2",
                "Assets/Isometric Pack 3d/Props/Camp1_Shield",
                "Assets/Isometric Pack 3d/Props/Camp1_Shooting_shield",
                "Assets/Isometric Pack 3d/Props/Camp1_Tower",
                "Assets/Isometric Pack 3d/Props/Camp2_Fierplace1",
                "Assets/Isometric Pack 3d/Props/Camp2_Fierplace2",
                "Assets/Isometric Pack 3d/Props/Camp2_tent",
                "Assets/Isometric Pack 3d/Props/Dungeon_Passage1",
                "Assets/Isometric Pack 3d/Props/Mine_Beam1",
                "Assets/Isometric Pack 3d/Props/Mine_Box",
                "Assets/Isometric Pack 3d/Props/Mine_Cart",
                "Assets/Isometric Pack 3d/Props/Mine_Enter",
                "Assets/Isometric Pack 3d/Props/Mine_Ore",
                "Assets/Isometric Pack 3d/Props/Mine_tracks1",
                "Assets/Isometric Pack 3d/Props/Mine_tracks2",
            },

            // ── 装饰 ──
            [HandMapCategory.Decoration] = new List<string>
            {
                "Assets/Isometric Pack 3d/Props/Banner_Pole1",
                "Assets/Isometric Pack 3d/Props/Banner_Pole2",
                "Assets/Isometric Pack 3d/Props/Banner_Pole3",
                "Assets/Isometric Pack 3d/Props/Banner_Pole4",
                "Assets/Isometric Pack 3d/Props/Barrier1",
                "Assets/Isometric Pack 3d/Props/Bench1",
                "Assets/Isometric Pack 3d/Props/Bench2",
                "Assets/Isometric Pack 3d/Props/Bucket",
                "Assets/Isometric Pack 3d/Props/Chest1A",
                "Assets/Isometric Pack 3d/Props/Chest1B",
                "Assets/Isometric Pack 3d/Props/Chest2A",
                "Assets/Isometric Pack 3d/Props/Chest2B",
                "Assets/Isometric Pack 3d/Props/Chest3A",
                "Assets/Isometric Pack 3d/Props/Chest3B",
                "Assets/Isometric Pack 3d/Props/Chest4A",
                "Assets/Isometric Pack 3d/Props/Chest4B",
                "Assets/Isometric Pack 3d/Props/Fence1_1",
                "Assets/Isometric Pack 3d/Props/Fence1_2",
                "Assets/Isometric Pack 3d/Props/Fence1_3",
                "Assets/Isometric Pack 3d/Props/Glow1",
                "Assets/Isometric Pack 3d/Props/GoldBag1",
                "Assets/Isometric Pack 3d/Props/Goldbag2",
                "Assets/Isometric Pack 3d/Props/Graveyard_01",
                "Assets/Isometric Pack 3d/Props/Graveyard_02",
                "Assets/Isometric Pack 3d/Props/Graveyard_03",
                "Assets/Isometric Pack 3d/Props/Graveyard_04",
                "Assets/Isometric Pack 3d/Props/Graveyard_05",
                "Assets/Isometric Pack 3d/Props/Graveyard_06",
                "Assets/Isometric Pack 3d/Props/Graveyard_07",
                "Assets/Isometric Pack 3d/Props/Graveyard_08",
                "Assets/Isometric Pack 3d/Props/Graveyard_09",
                "Assets/Isometric Pack 3d/Props/Graveyard_10",
                "Assets/Isometric Pack 3d/Props/Graveyard_12",
                "Assets/Isometric Pack 3d/Props/Graveyard_13",
                "Assets/Isometric Pack 3d/Props/Graveyard_14",
                "Assets/Isometric Pack 3d/Props/Graveyard_15",
                "Assets/Isometric Pack 3d/Props/Graveyard_16",
                "Assets/Isometric Pack 3d/Props/Graveyard_17",
                "Assets/Isometric Pack 3d/Props/Graveyard_Urn1",
                "Assets/Isometric Pack 3d/Props/Graveyard_Urn2",
                "Assets/Isometric Pack 3d/Props/Ground_Beams1",
                "Assets/Isometric Pack 3d/Props/Ground_Beams2",
                "Assets/Isometric Pack 3d/Props/Lamp_01",
                "Assets/Isometric Pack 3d/Props/Magic_Orb",
                "Assets/Isometric Pack 3d/Props/Magic_Pillar1",
                "Assets/Isometric Pack 3d/Props/Magic_Pillar2",
                "Assets/Isometric Pack 3d/Props/Magic_Pillar3",
                "Assets/Isometric Pack 3d/Props/Magic_Pillar4",
                "Assets/Isometric Pack 3d/Props/Magic_Pillar5",
                "Assets/Isometric Pack 3d/Props/Magic_Shrine1",
                "Assets/Isometric Pack 3d/Props/Magic_Shrine2",
                "Assets/Isometric Pack 3d/Props/Sign1",
                "Assets/Isometric Pack 3d/Props/Sign2",
                "Assets/Isometric Pack 3d/Props/Waterfall1",
                "Assets/Isometric Pack 3d/Props/Waterfall2",
                "Assets/Isometric Pack 3d/Props/Well1",
                "Assets/Isometric Pack 3d/Props/Wood_el_01",
                "Assets/Isometric Pack 3d/Props/Wood_el_02",
                "Assets/Isometric Pack 3d/Props/Wood_el_03",
                "Assets/Isometric Pack 3d/Props/Wood_el_04",
                "Assets/Isometric Pack 3d/Props/Wood_el_05",
                "Assets/Isometric Pack 3d/Props/Wood_el_06",
            },

            // ── 特效 ──
            [HandMapCategory.Effect] = new List<string>
            {
                "Assets/Isometric Pack 3d/Particles/Candleflame",
                "Assets/Isometric Pack 3d/Particles/Fog1",
                "Assets/Isometric Pack 3d/Particles/Fog2",
                "Assets/Isometric Pack 3d/Particles/Fog3",
                "Assets/Isometric Pack 3d/Particles/Glow1",
                "Assets/Isometric Pack 3d/Particles/Ripples",
            },

            // 擦除：调色板里用空槽表示
            [HandMapCategory.Erase] = new List<string>(),
        };

        // 把 Prefab 路径转换为 prefab 的显示名（去掉 .prefab + 路径前缀）
        public static string GetDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "(无)";
            int slash = path.LastIndexOf('/');
            string name = slash >= 0 ? path.Substring(slash + 1) : path;
            return name;
        }
    }
}