using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RollOfLegends.EditorTools
{
    /// <summary>
    /// The landscape around the board: an island of grass hexagons in a calm sea, with a small lake, mountains and hills in
    /// the distance, trees and rocks, a blue and a red village, drifting clouds, a gradient sky and fog.
    /// Models: KayKit Medieval Hexagon Pack (CC0), the same pack the castles come from.
    /// Spots are given in screen directions: "right" = to the right on screen, "far" = up the screen.
    /// A fixed random seed keeps the landscape the same on every rebuild (change it for a new layout).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        private const string SceneryFolder = "KayKit/Hexagon";
        private const int SceneryRandomSeed = 2026;
        private const float SceneryScale = 0.9f;
        private const float GroundTop = BoardTop - 0.14f; // top of the grass hexagons, just under the board plate
        private const float BoardKeepOut = 6.4f;          // nothing is placed closer to the middle than this (x and z)

        private static readonly Color SkyTop = Hex("#5aa6f2");
        private static readonly Color SkyHorizon = Hex("#d4e9f7");
        private static readonly Color SeaColor = Hex("#3f8fcf");

        private static Vector3 ScreenRight => Quaternion.Euler(0f, CameraYaw, 0f) * Vector3.right;
        private static Vector3 ScreenFar => Quaternion.Euler(0f, CameraYaw, 0f) * Vector3.forward;

        private static readonly Vector2 LakeSpot = new Vector2(0.5f, 12f);
        private const float LakeRadius = 2.6f;
        private static readonly Vector2 BlueVillageSpot = new Vector2(-8.5f, 9f);
        private static readonly Vector2 RedVillageSpot = new Vector2(8.5f, 9f);

        /// <summary>A ground point from screen directions (right, far).</summary>
        private static Vector3 Spot(float right, float far)
        {
            return ScreenRight * right + ScreenFar * far + Vector3.up * GroundTop;
        }

        private static Vector3 Spot(Vector2 spot)
        {
            return Spot(spot.x, spot.y);
        }

        private static void CreateScenery()
        {
            var random = new System.Random(SceneryRandomSeed);
            Transform root = new GameObject("Scenery").transform;

            CreateSkyAndFog();
            CreateHexGround(root, random);
            CreateMountainsAndHills(root);
            CreateVillage(root, BlueVillageSpot, "blue", mirrored: false);
            CreateVillage(root, RedVillageSpot, "red", mirrored: true);
            CreateLakePlants(root, random);
            ScatterNature(root, random);
            CreateClouds(root, random);
        }

        // ---------- Sky ----------

        private static void CreateSkyAndFog()
        {
            string path = $"{MaterialsFolder}/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("RollOfLegends/GradientSky"));
                sky.SetColor("_TopColor", SkyTop);
                sky.SetColor("_HorizonColor", SkyHorizon);
                AssetDatabase.CreateAsset(sky, path);
            }
            RenderSettings.skybox = sky;

            // CameraFit moves the fog to start just behind the board.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky.GetColor("_HorizonColor");
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 70f;
        }

        // ---------- Ground ----------

        /// <summary>Grass hexagons all around the board (water ones make the lake), fraying out at the edge.</summary>
        private static void CreateHexGround(Transform root, System.Random random)
        {
            Transform ground = new GameObject("HexGround").transform;
            ground.SetParent(root, false);

            // Measure one tile to build the grid from its real size.
            GameObject probe = Prop(ground, "Tiles/hex_grass.fbx", Vector3.zero, 0f);
            Bounds size = ModelBounds(probe);
            float top = size.max.y - probe.transform.position.y;
            Object.DestroyImmediate(probe);

            // Corners pointing left/right ("flat top") or up/down ("pointy top") decide how rows interlock.
            bool flatTop = size.size.x >= size.size.z;
            float columnStep = flatTop ? size.size.x * 0.75f : size.size.x;
            float rowStep = flatTop ? size.size.z : size.size.z * 0.75f;
            Vector3 lake = Spot(LakeSpot);

            const int reach = 20;
            for (int column = -reach; column <= reach; column++)
            {
                for (int row = -reach; row <= reach; row++)
                {
                    float x = column * columnStep + (!flatTop && (row & 1) != 0 ? columnStep / 2f : 0f);
                    float z = row * rowStep + (flatTop && (column & 1) != 0 ? rowStep / 2f : 0f);
                    if (Mathf.Abs(x) < 4.4f && Mathf.Abs(z) < 4.4f) continue; // fully hidden under the board plate

                    float distance = new Vector2(x, z).magnitude;
                    if (distance > 20f + (float)random.NextDouble() * 3f) continue; // ragged outer edge

                    bool water = new Vector2(x - lake.x, z - lake.z).magnitude < LakeRadius;
                    // Tiles further out step up and down a little, which looks more natural.
                    float bump = distance > 8.5f && !water ? -(float)random.NextDouble() * 0.12f : 0f;
                    var position = new Vector3(x, GroundTop - top + bump, z);
                    Prop(ground, water ? "Tiles/hex_water.fbx" : "Tiles/hex_grass.fbx", position, 0f, alignBase: false);
                }
            }
        }

        // ---------- Landmarks ----------

        private static void CreateMountainsAndHills(Transform root)
        {
            Transform group = new GameObject("MountainsAndHills").transform;
            group.SetParent(root, false);

            // Far away along the top of the screen; fog makes the furthest ones fade into the sky.
            var mountains = new (string model, float right, float far, float scale, float yaw)[]
            {
                ("mountain_B_grass_trees", -17f, 13f, 2.3f, 20f),
                ("mountain_A", -12f, 18f, 3.2f, 0f),
                ("mountain_C_grass_trees", -6.5f, 17f, 2.6f, 140f),
                ("mountain_B", 0f, 21f, 3.6f, 60f),
                ("mountain_A_grass_trees", 6.5f, 17f, 2.6f, 200f),
                ("mountain_A", 12.5f, 19f, 3.2f, 100f),
                ("mountain_C_grass_trees", 17f, 13f, 2.3f, 300f),
                ("mountain_B_grass_trees", -22f, 6f, 2.2f, 90f),
                ("mountain_A_grass_trees", 22f, 6f, 2.2f, 250f),
            };
            foreach ((string model, float right, float far, float scale, float yaw) in mountains)
            {
                Prop(group, $"Nature/{model}.fbx", Spot(right, far), yaw, scale);
            }

            var hills = new (string model, float right, float far, float scale, float yaw)[]
            {
                ("hills_A_trees", -13f, 5f, 1.6f, 30f),
                ("hills_B_trees", 13f, 4f, 1.6f, 160f),
                ("hills_C_trees", -4.5f, 14.5f, 1.4f, 80f),
                ("hills_A_trees", 4.5f, 14.5f, 1.4f, 260f),
                ("hill_single_A", -11f, 13f, 1.3f, 0f),
                ("hill_single_B", 11f, 13f, 1.3f, 120f),
                ("hill_single_C", -14f, -3f, 1.3f, 200f),
                ("hill_single_A", 14f, -4f, 1.3f, 70f),
            };
            foreach ((string model, float right, float far, float scale, float yaw) in hills)
            {
                Prop(group, $"Nature/{model}.fbx", Spot(right, far), yaw, scale);
            }
        }

        /// <summary>A few houses, a well and a windmill in team colors, next to the far corners of the board.</summary>
        private static void CreateVillage(Transform root, Vector2 center, string color, bool mirrored)
        {
            Transform village = new GameObject($"Village_{color}").transform;
            village.SetParent(root, false);
            float side = mirrored ? -1f : 1f;
            float facing = CameraYaw + 180f; // houses turn their fronts toward the camera

            Prop(village, $"Buildings/building_windmill_{color}.fbx", Spot(center + new Vector2(-1.2f * side, 1.6f)), facing + 20f * side);
            Prop(village, $"Buildings/building_home_A_{color}.fbx", Spot(center + new Vector2(1.1f * side, -0.6f)), facing - 15f * side);
            Prop(village, $"Buildings/building_home_B_{color}.fbx", Spot(center + new Vector2(-1.6f * side, -1.1f)), facing + 10f * side);
            Prop(village, $"Buildings/building_well_{color}.fbx", Spot(center + new Vector2(0.6f * side, 1.4f)), facing);
        }

        private static void CreateLakePlants(Transform root, System.Random random)
        {
            Transform plants = new GameObject("LakePlants").transform;
            plants.SetParent(root, false);
            Vector3 lake = Spot(LakeSpot);
            for (int i = 0; i < 5; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = LakeRadius * (0.3f + (float)random.NextDouble() * 0.5f);
                Vector3 position = lake + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                string model = i % 2 == 0 ? "Nature/waterlily_A.fbx" : "Nature/waterplant_A.fbx";
                Prop(plants, model, position, (float)random.NextDouble() * 360f, alignBase: false);
            }
        }

        /// <summary>Tree clusters and rocks scattered around, keeping clear of the board, the lake and the villages.</summary>
        private static void ScatterNature(Transform root, System.Random random)
        {
            Transform nature = new GameObject("TreesAndRocks").transform;
            nature.SetParent(root, false);

            string[] trees =
            {
                "trees_A_large", "trees_A_medium", "trees_A_small", "trees_B_large", "trees_B_medium", "trees_B_small",
                "tree_single_A", "tree_single_B",
            };
            string[] rocks = { "rock_single_A", "rock_single_B", "rock_single_C", "rock_single_D", "rock_single_E" };

            var taken = new System.Collections.Generic.List<(Vector3 position, float radius)>
            {
                (Spot(LakeSpot), LakeRadius + 0.6f),
                (Spot(BlueVillageSpot), 3f),
                (Spot(RedVillageSpot), 3f),
            };

            PlaceScattered(nature, random, trees, count: 46, spacing: 1.5f, minScale: 0.9f, maxScale: 1.3f, taken);
            PlaceScattered(nature, random, rocks, count: 18, spacing: 1f, minScale: 0.8f, maxScale: 1.4f, taken);
        }

        private static void PlaceScattered(Transform parent, System.Random random, string[] models, int count, float spacing,
            float minScale, float maxScale, System.Collections.Generic.List<(Vector3 position, float radius)> taken)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
            {
                float right = Mathf.Lerp(-19f, 19f, (float)random.NextDouble());
                float far = Mathf.Lerp(-11f, 15f, (float)random.NextDouble());
                Vector3 position = Spot(right, far);
                if (Mathf.Abs(position.x) < BoardKeepOut && Mathf.Abs(position.z) < BoardKeepOut) continue;
                if (position.magnitude > 17f) continue; // stay on the hexagons

                bool free = true;
                foreach ((Vector3 other, float radius) in taken)
                {
                    if (Vector3.Distance(position, other) < radius + spacing * 0.5f) free = false;
                }
                if (!free) continue;

                string model = models[random.Next(models.Length)];
                float scale = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
                Prop(parent, $"Nature/{model}.fbx", position, (float)random.NextDouble() * 360f, scale);
                taken.Add((position, spacing * 0.5f));
                placed++;
            }
        }

        private static void CreateClouds(Transform root, System.Random random)
        {
            Transform clouds = new GameObject("Clouds").transform;
            clouds.SetParent(root, false);
            for (int i = 0; i < 7; i++)
            {
                float right = Mathf.Lerp(-18f, 18f, i / 6f) + (float)random.NextDouble() * 3f;
                float far = Mathf.Lerp(15f, 24f, (float)random.NextDouble());
                Vector3 position = Spot(right, far) + Vector3.up * Mathf.Lerp(6f, 8.5f, (float)random.NextDouble());
                string model = random.Next(2) == 0 ? "Nature/cloud_big.fbx" : "Nature/cloud_small.fbx";
                GameObject cloud = Prop(clouds, model, position, (float)random.NextDouble() * 360f,
                    Mathf.Lerp(1.6f, 2.6f, (float)random.NextDouble()), alignBase: false, castShadows: false, isStatic: false);
                cloud.AddComponent<Drift>().Setup(ScreenRight * Mathf.Lerp(0.15f, 0.35f, (float)random.NextDouble()), 20f);
            }
        }

        // ---------- Helpers ----------

        /// <summary>
        /// The pack's color atlas, but with its yellow-lime grass swapped for green (hexagons_medieval_green.png),
        /// so the meadow looks fresh under our warm light. The castles keep the original atlas.
        /// </summary>
        private static Material SceneryMaterial()
        {
            Material material = GetOrCreateMaterial("Models/HexagonsGreen", Color.white, smoothness: 0.15f);
            if (material.GetTexture("_BaseMap") == null)
            {
                string texture = $"{ModelsFolder}/{SceneryFolder}/hexagons_medieval_green.png";
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        /// <summary>
        /// Places a scenery model (path inside the Hexagon folder) with the given turn and size. With alignBase its
        /// lowest point rests on <paramref name="position"/>. Scenery never moves, so it is marked static (Unity
        /// can then draw many pieces together, which is faster on phones).
        /// </summary>
        private static GameObject Prop(Transform parent, string model, Vector3 position, float yaw, float scale = 1f,
            bool alignBase = true, bool castShadows = true, bool isStatic = true)
        {
            string path = $"{ModelsFolder}/{SceneryFolder}/{model}";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new System.IO.FileNotFoundException($"Scenery model not found: {path}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetParent(parent, false);
            // Keep the model's own root turn and size (Blender exports lie on their back without it).
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f) * asset.transform.localRotation);
            instance.transform.localScale = asset.transform.localScale * SceneryScale * scale;
            if (alignBase)
            {
                float bottom = ModelBounds(instance).min.y;
                instance.transform.position += Vector3.up * (position.y - bottom);
            }

            Material material = SceneryMaterial();
            foreach (Renderer part in instance.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[part.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                part.sharedMaterials = materials;
                if (!castShadows) part.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (isStatic)
            {
                foreach (Transform part in instance.GetComponentsInChildren<Transform>())
                {
                    GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic);
                }
            }
            return instance;
        }
    }
}
