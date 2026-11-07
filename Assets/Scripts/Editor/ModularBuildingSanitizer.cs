using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Han.EditorTools
{
    /// <summary>
    /// ModularBuildingSanitizer
    /// Comprehensive inspection, trimming, grid-snapping, and collider cleanup tool
    /// for modular building assets (walls, floors, columns, doorframes, roofs).
    /// </summary>
    public class ModularBuildingSanitizer : EditorWindow
    {
        private Vector2 scrollPos;
        private Transform buildingRoot;
        private int selectedTab = 0;
        private readonly string[] tabNames = new string[] { "Overview & Audit", "Duplicate Purge", "Grid & Seam Snapper", "Mesh Trimmer", "Collider Rebuilder", "Normal Smoother" };

        // Snapping Settings
        private float gridPitchXZ = 0.5f;
        private float firstFloorY = 0.04f;
        private float secondFloorY = 4.04f;
        private bool snapPositionX = true;
        private bool snapPositionY = true;
        private bool snapPositionZ = true;
        private bool snapRotations90 = true;

        // Trimming Settings
        private float wallNominalWidth = 6.0f;
        private float wallNominalHeight = 4.0f;
        private float platformNominalWidth = 6.0f;
        private float platformNominalLength = 4.0f;
        private float platformThickness = 0.20f;

        // Audit statistics
        private int totalMeshes = 0;
        private int totalMeshColliders = 0;
        private int totalBoxColliders = 0;
        private int duplicateCount = 0;
        private List<string> duplicateList = new List<string>();

        [MenuItem("Tools/Han Tavern/Modular Building Sanitizer & Snapper", false, 10)]
        public static void ShowWindow()
        {
            var win = GetWindow<ModularBuildingSanitizer>("Building Sanitizer");
            win.minSize = new Vector2(620, 580);
            win.FindBuildingRoot();
            win.RunAudit();
        }

        private void OnEnable()
        {
            FindBuildingRoot();
            RunAudit();
        }

        private void FindBuildingRoot()
        {
            if (buildingRoot == null)
            {
                var go = GameObject.Find("Building");
                if (go != null) buildingRoot = go.transform;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Modular Building Inspector & Sanitizer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Inspects, trims, snaps, and fixes modular assets (floors, walls, ceilings, columns, doorframes) to eliminate micro-crevices, overlapping z-fighting faces, and collider lips.", MessageType.Info);

            EditorGUILayout.Space(4);
            buildingRoot = (Transform)EditorGUILayout.ObjectField("Building Root:", buildingRoot, typeof(Transform), true);

            if (buildingRoot == null)
            {
                EditorGUILayout.HelpBox("Select or assign the 'Building' root GameObject in the hierarchy.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(6);
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(28));
            EditorGUILayout.Space(6);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (selectedTab)
            {
                case 0: DrawOverviewTab(); break;
                case 1: DrawDuplicatePurgeTab(); break;
                case 2: DrawSnapperTab(); break;
                case 3: DrawMeshTrimmerTab(); break;
                case 4: DrawColliderRebuilderTab(); break;
                case 5: DrawNormalSmootherTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // =========================================================================
        // TAB 0: OVERVIEW & AUDIT
        // =========================================================================
        private void DrawOverviewTab()
        {
            EditorGUILayout.LabelField("Scene Audit & Statistics", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Building Transform:", $"{buildingRoot.name} (Children: {buildingRoot.childCount})");
            EditorGUILayout.LabelField($"Total Mesh Filters:", $"{totalMeshes}");
            EditorGUILayout.LabelField($"Unoptimized MeshColliders:", $"{totalMeshColliders} (Creates seam snagging lips)");
            EditorGUILayout.LabelField($"Optimized BoxColliders:", $"{totalBoxColliders}");
            EditorGUILayout.LabelField($"Duplicate / Overlapping Meshes:", $"{duplicateCount}", duplicateCount > 0 ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Re-Run Diagnostic Audit", GUILayout.Height(30)))
            {
                RunAudit();
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Identified Critical Structural Issues:", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "1. Extraneous FBX Overhangs:\n" +
                "   - 'platform.fbx' has a width of 6.10m instead of 6.00m (+10cm overhang).\n" +
                "   - 'wood_wall.021.fbx' has a width of 6.15m (+15cm overhang past vertical posts).\n" +
                "   - Causes wall timbers and floor planks to penetrate adjacent modules, creating coplanar z-fighting and protruding lips outside corner joints.\n\n" +
                "2. Duplicate Tile Instances:\n" +
                "   - 'Roof (2)' contains 29 duplicate roof tiles overlapping 'Roof' at dist 0.0000m.\n" +
                "   - 'First Floor/Floor' contains 'platform (65)' overlapping 'platform (2)'.\n" +
                "   - 'wood_column.004 (2)' overlaps 'wood_column.004 (1)' at dist 0.0000m.\n\n" +
                "3. Micro-Crevices & Float Inaccuracies:\n" +
                "   - Placed tiles exhibit random micrometer offsets (e.g. Y = -0.025m, 0.065m, 0.098m; Z = 76.46m vs 76.50m).\n" +
                "   - Causes visible seam gaps through which lighting leaks or void space is visible.\n\n" +
                "4. Non-Convex MeshCollider Snagging:\n" +
                "   - 498 individual MeshColliders on modular tiles create micro-ledges that snag the player movement and NPC NavMesh agents.",
                MessageType.Warning);
        }

        private void RunAudit()
        {
            if (buildingRoot == null) return;

            var meshes = buildingRoot.GetComponentsInChildren<MeshFilter>(true);
            totalMeshes = meshes.Length;

            var cols = buildingRoot.GetComponentsInChildren<Collider>(true);
            totalMeshColliders = cols.OfType<MeshCollider>().Count();
            totalBoxColliders = cols.OfType<BoxCollider>().Count();

            duplicateList.Clear();
            for (int i = 0; i < meshes.Length; i++)
            {
                var m1 = meshes[i];
                var t1 = m1.transform;
                if (t1.name.Contains("BlurOutline")) continue;

                for (int j = i + 1; j < meshes.Length; j++)
                {
                    var m2 = meshes[j];
                    var t2 = m2.transform;
                    if (t2.name.Contains("BlurOutline") || t1.parent == t2 || t2.parent == t1) continue;

                    if (m1.sharedMesh == m2.sharedMesh && m1.sharedMesh != null)
                    {
                        float d = Vector3.Distance(t1.position, t2.position);
                        float a = Quaternion.Angle(t1.rotation, t2.rotation);
                        if (d < 0.04f && (a < 5f || Mathf.Abs(a - 180f) < 5f))
                        {
                            duplicateList.Add($"{t1.name} <=> {t2.name} at {t1.position:F2} (d={d:F4}m)");
                        }
                    }
                }
            }
            duplicateCount = duplicateList.Count;
        }

        // =========================================================================
        // TAB 1: DUPLICATE PURGE
        // =========================================================================
        private void DrawDuplicatePurgeTab()
        {
            EditorGUILayout.LabelField("Duplicate & Z-Fighting Purge", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Deletes redundant identical duplicate meshes sitting at the exact same location (such as duplicate Roof (2), overlapping floor tiles, and duplicate columns).", MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Detected Exact / Near Duplicates: {duplicateCount}");

            if (duplicateCount > 0)
            {
                EditorGUILayout.BeginVertical("box", GUILayout.MaxHeight(180));
                foreach (var dup in duplicateList.Take(20))
                {
                    EditorGUILayout.LabelField(dup, EditorStyles.miniLabel);
                }
                if (duplicateList.Count > 20)
                {
                    EditorGUILayout.LabelField($"... and {duplicateList.Count - 20} more duplicate pairs.", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(8);
                GUI.backgroundColor = new Color(0.9f, 0.4f, 0.3f);
                if (GUILayout.Button("Purge Duplicate Objects & Overlapping Tiles (Undoable)", GUILayout.Height(34)))
                {
                    PurgeDuplicates();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox("Zero duplicate meshes detected in the building hierarchy. Scene is clean!", MessageType.Info);
            }
        }

        private void PurgeDuplicates()
        {
            if (buildingRoot == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Purge Modular Duplicates");
            int undoGroup = Undo.GetCurrentGroup();

            int removed = 0;

            // 1. Check if 'Roof (2)' exists (pure clone of Roof)
            var roof2 = buildingRoot.Find("Roof (2)");
            if (roof2 != null)
            {
                Undo.DestroyObjectImmediate(roof2.gameObject);
                removed++;
                Debug.Log("[ModularSanitizer] Removed redundant 'Roof (2)' duplicate container.");
            }

            // 2. Scan remaining MeshFilters
            var meshes = buildingRoot.GetComponentsInChildren<MeshFilter>(true);
            var toDestroy = new HashSet<GameObject>();

            for (int i = 0; i < meshes.Length; i++)
            {
                var m1 = meshes[i];
                if (m1 == null || toDestroy.Contains(m1.gameObject)) continue;
                var t1 = m1.transform;
                if (t1.name.Contains("BlurOutline")) continue;

                for (int j = i + 1; j < meshes.Length; j++)
                {
                    var m2 = meshes[j];
                    if (m2 == null || toDestroy.Contains(m2.gameObject)) continue;
                    var t2 = m2.transform;
                    if (t2.name.Contains("BlurOutline") || t1.parent == t2 || t2.parent == t1) continue;

                    if (m1.sharedMesh == m2.sharedMesh && m1.sharedMesh != null)
                    {
                        float d = Vector3.Distance(t1.position, t2.position);
                        float a = Quaternion.Angle(t1.rotation, t2.rotation);
                        if (d < 0.04f && (a < 5f || Mathf.Abs(a - 180f) < 5f))
                        {
                            // Keep m1, mark m2 for removal
                            toDestroy.Add(m2.gameObject);
                        }
                    }
                }
            }

            foreach (var go in toDestroy)
            {
                if (go != null)
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            RunAudit();
            Debug.Log($"[ModularSanitizer] Purged {removed} duplicate / z-fighting objects.");
        }

        // =========================================================================
        // TAB 2: GRID & SEAM SNAPPER
        // =========================================================================
        private void DrawSnapperTab()
        {
            EditorGUILayout.LabelField("Precision Grid & Seam Snapper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Snaps floor platforms and wall segments to precise grid increments and uniform floor elevations, eliminating micro-crevices and hairline gaps.", MessageType.Info);

            EditorGUILayout.Space(6);
            gridPitchXZ = EditorGUILayout.FloatField("Horizontal Grid Pitch (XZ):", gridPitchXZ);
            firstFloorY = EditorGUILayout.FloatField("First Floor Ground Elevation (Y):", firstFloorY);
            secondFloorY = EditorGUILayout.FloatField("Second Floor Elevation (Y):", secondFloorY);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            snapPositionX = EditorGUILayout.ToggleLeft("Snap X", snapPositionX, GUILayout.Width(80));
            snapPositionY = EditorGUILayout.ToggleLeft("Snap Y", snapPositionY, GUILayout.Width(80));
            snapPositionZ = EditorGUILayout.ToggleLeft("Snap Z", snapPositionZ, GUILayout.Width(80));
            snapRotations90 = EditorGUILayout.ToggleLeft("Snap Euler to 90°", snapRotations90);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.5f);
            if (GUILayout.Button("Batch-Snap Floors (Eliminate Floor Crevices)", GUILayout.Height(32)))
            {
                BatchSnapFloors();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Batch-Snap Walls (Flush Seam Alignment)", GUILayout.Height(32)))
            {
                BatchSnapWalls();
            }
            GUI.backgroundColor = Color.white;
        }

        private void BatchSnapFloors()
        {
            if (buildingRoot == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Snap Modular Floors");
            int group = Undo.GetCurrentGroup();

            int snapped = 0;
            var f1 = buildingRoot.Find("First Floor/Floor");
            if (f1 != null)
            {
                for (int i = 0; i < f1.childCount; i++)
                {
                    var t = f1.GetChild(i);
                    Undo.RecordObject(t, "Snap Floor Tile");
                    SnapTransform(t, firstFloorY);
                    snapped++;
                }
            }

            var f2 = buildingRoot.Find("Second Floor/Floor");
            if (f2 != null)
            {
                for (int i = 0; i < f2.childCount; i++)
                {
                    var t = f2.GetChild(i);
                    Undo.RecordObject(t, "Snap Floor Tile");
                    SnapTransform(t, secondFloorY);
                    snapped++;
                }
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[ModularSanitizer] Successfully snapped {snapped} floor tiles to precision grid.");
        }

        private void BatchSnapWalls()
        {
            if (buildingRoot == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Snap Modular Walls");
            int group = Undo.GetCurrentGroup();

            int snapped = 0;
            var w1 = buildingRoot.Find("First Floor/Walls");
            if (w1 != null)
            {
                for (int i = 0; i < w1.childCount; i++)
                {
                    var t = w1.GetChild(i);
                    Undo.RecordObject(t, "Snap Wall");
                    SnapTransform(t, null);
                    snapped++;
                }
            }

            var w2 = buildingRoot.Find("Second Floor/Walls");
            if (w2 != null)
            {
                for (int i = 0; i < w2.childCount; i++)
                {
                    var t = w2.GetChild(i);
                    Undo.RecordObject(t, "Snap Wall");
                    SnapTransform(t, null);
                    snapped++;
                }
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[ModularSanitizer] Successfully snapped {snapped} wall segments to precision grid.");
        }

        private void SnapTransform(Transform t, float? targetY)
        {
            Vector3 pos = t.position;
            if (snapPositionX) pos.x = Mathf.Round(pos.x / gridPitchXZ) * gridPitchXZ;
            if (snapPositionZ) pos.z = Mathf.Round(pos.z / gridPitchXZ) * gridPitchXZ;
            if (targetY.HasValue) pos.y = targetY.Value;
            else if (snapPositionY) pos.y = Mathf.Round(pos.y / 0.1f) * 0.1f;
            t.position = pos;

            if (snapRotations90)
            {
                Vector3 euler = t.rotation.eulerAngles;
                euler.x = Mathf.Round(euler.x / 90f) * 90f;
                // Support both 90° orthogonal and 45° diagonal corner walls
                euler.y = Mathf.Round(euler.y / 45f) * 45f;
                euler.z = Mathf.Round(euler.z / 90f) * 90f;
                t.rotation = Quaternion.Euler(euler);
            }
        }

        // =========================================================================
        // TAB 3: MESH TRIMMER & CLAMPER
        // =========================================================================
        private void DrawMeshTrimmerTab()
        {
            EditorGUILayout.LabelField("Asset Trimming & Clean Mesh Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Clamps extraneous geometry extending past the boundary planes ([-3.0m, +3.0m] for walls and floors), producing clean trimmed mesh assets without destructive changes to the source FBXs.", MessageType.Info);

            EditorGUILayout.Space(6);
            wallNominalWidth = EditorGUILayout.FloatField("Wall Nominal Width (m):", wallNominalWidth);
            wallNominalHeight = EditorGUILayout.FloatField("Wall Nominal Height (m):", wallNominalHeight);
            platformNominalWidth = EditorGUILayout.FloatField("Platform Width (m):", platformNominalWidth);
            platformNominalLength = EditorGUILayout.FloatField("Platform Length (m):", platformNominalLength);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Generate Trimmed Clean Meshes", GUILayout.Height(34)))
            {
                GenerateCleanTrimmedMeshes();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Apply Trimmed Meshes to Scene Placements", GUILayout.Height(30)))
            {
                ApplyCleanTrimmedMeshesToScene();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Create Clean Prefab Variants (Assets/Prefabs/CleanModular)", GUILayout.Height(30)))
            {
                GenerateCleanPrefabVariants();
            }
        }

        private void GenerateCleanTrimmedMeshes()
        {
            string outDir = "Assets/Meshes/CleanModular";
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // Trim platform
            TrimMeshAsset("Assets/Mega Fantasy Props Pack/FBX/platform.fbx", $"{outDir}/platform_Clean.asset", -3.0f, 3.0f, false);
            TrimMeshAsset("Assets/Mega Fantasy Props Pack/FBX/platform.005.fbx", $"{outDir}/platform_005_Clean.asset", -3.0f, 3.0f, false);

            // Trim walls
            TrimMeshAsset("Assets/Mega Fantasy Props Pack/FBX/wood_wall.021.fbx", $"{outDir}/wood_wall_021_Clean.asset", -3.0f, 3.0f, true);
            TrimMeshAsset("Assets/Mega Fantasy Props Pack/FBX/wood_wall_window.001.fbx", $"{outDir}/wood_wall_window_001_Clean.asset", -3.0f, 3.0f, true);
            TrimMeshAsset("Assets/Mega Fantasy Props Pack/FBX/wood_wall_enter.002.fbx", $"{outDir}/wood_wall_enter_002_Clean.asset", -3.0f, 3.0f, true);
            TrimMeshAsset("Assets/Mega Fantasy Props Pack/FBX/wood_wall.009.fbx", $"{outDir}/wood_wall_009_Clean.asset", -3.0f, 3.0f, true);
            TrimMeshAsset("Assets/Mega Fantasy Props Pack/FBX/wood_wall.003.fbx", $"{outDir}/wood_wall_003_Clean.asset", -3.0f, 9.0f, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Clean Meshes Generated", $"Trimmed modular meshes successfully saved to:\n{outDir}", "OK");
        }

        private void TrimMeshAsset(string srcFbxPath, string destAssetPath, float minX, float maxX, bool alignWallY)
        {
            var src = AssetDatabase.LoadAssetAtPath<Mesh>(srcFbxPath);
            if (src == null)
            {
                Debug.LogWarning($"[ModularSanitizer] Source mesh not found: {srcFbxPath}");
                return;
            }

            Mesh copy = UnityEngine.Object.Instantiate(src);
            copy.name = Path.GetFileNameWithoutExtension(destAssetPath);

            var verts = copy.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                if (verts[i].x < minX) verts[i].x = minX;
                else if (verts[i].x > maxX) verts[i].x = maxX;

                if (alignWallY)
                {
                    // Align wall base to 0.00m
                    verts[i].y += 0.125f;
                }
            }

            copy.vertices = verts;
            copy.RecalculateBounds();
            copy.RecalculateNormals();
            copy.RecalculateTangents();

            AssetDatabase.CreateAsset(copy, destAssetPath);
            Debug.Log($"[ModularSanitizer] Created trimmed mesh: {destAssetPath} (bounds size: {copy.bounds.size})");
        }

        private void GenerateCleanPrefabVariants()
        {
            string prefabDir = "Assets/Prefabs/CleanModular";
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

            var cleanPlatform = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/platform_Clean.asset");
            var cleanWall = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/wood_wall_021_Clean.asset");
            var cleanWindow = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/wood_wall_window_001_Clean.asset");
            var cleanDoor = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/wood_wall_enter_002_Clean.asset");

            void CreateVariant(string basePrefabPath, string destPrefabPath, Mesh cleanMesh, Action<GameObject> configureCollider)
            {
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
                if (basePrefab == null)
                {
                    Debug.LogWarning($"[ModularSanitizer] Base prefab not found at {basePrefabPath}");
                    return;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                var mf = instance.GetComponent<MeshFilter>();
                if (mf != null && cleanMesh != null) mf.sharedMesh = cleanMesh;

                var mc = instance.GetComponent<MeshCollider>();
                if (mc != null) UnityEngine.Object.DestroyImmediate(mc);

                configureCollider?.Invoke(instance);

                PrefabUtility.SaveAsPrefabAsset(instance, destPrefabPath);
                UnityEngine.Object.DestroyImmediate(instance);
                Debug.Log($"[ModularSanitizer] Created Prefab Variant: {destPrefabPath}");
            }

            CreateVariant("Assets/Mega Fantasy Props Pack/Prefabs/Platforms/platform.prefab",
                $"{prefabDir}/platform_Clean.prefab", cleanPlatform, go =>
                {
                    var bc = go.GetComponent<BoxCollider>();
                    if (bc == null) bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(6.0f, 0.20f, 4.0f);
                    bc.center = new Vector3(0f, -0.10f, 2.0f);
                });

            CreateVariant("Assets/Mega Fantasy Props Pack/Prefabs/Walls/wood_wall.021.prefab",
                $"{prefabDir}/wood_wall_021_Clean.prefab", cleanWall, go =>
                {
                    var bc = go.GetComponent<BoxCollider>();
                    if (bc == null) bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(6.0f, 4.0f, 0.40f);
                    bc.center = new Vector3(0f, 2.0f, 0f);
                });

            CreateVariant("Assets/Mega Fantasy Props Pack/Prefabs/Walls/Windows/wood_wall_window.001.prefab",
                $"{prefabDir}/wood_wall_window_001_Clean.prefab", cleanWindow, go =>
                {
                    var bc = go.GetComponent<BoxCollider>();
                    if (bc == null) bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(6.0f, 4.0f, 0.40f);
                    bc.center = new Vector3(0f, 2.0f, 0f);
                });

            CreateVariant("Assets/Mega Fantasy Props Pack/Prefabs/Walls/Doors/wood_wall_enter.002.prefab",
                $"{prefabDir}/wood_wall_enter_002_Clean.prefab", cleanDoor, go =>
                {
                    var boxes = go.GetComponents<BoxCollider>();
                    foreach (var b in boxes) UnityEngine.Object.DestroyImmediate(b);

                    var left = go.AddComponent<BoxCollider>();
                    left.size = new Vector3(1.8f, 4.0f, 0.40f);
                    left.center = new Vector3(-2.1f, 2.0f, 0f);

                    var right = go.AddComponent<BoxCollider>();
                    right.size = new Vector3(1.8f, 4.0f, 0.40f);
                    right.center = new Vector3(2.1f, 2.0f, 0f);

                    var lintel = go.AddComponent<BoxCollider>();
                    lintel.size = new Vector3(2.4f, 1.2f, 0.40f);
                    lintel.center = new Vector3(0f, 3.4f, 0f);
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Prefab Variants Created", $"Clean Prefab Variants created in:\n{prefabDir}", "OK");
        }

        private void ApplyCleanTrimmedMeshesToScene()
        {
            if (buildingRoot == null) return;

            var cleanPlatform = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/platform_Clean.asset");
            var cleanPlatform005 = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/platform_005_Clean.asset");
            var cleanWall = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/wood_wall_021_Clean.asset");
            var cleanWindow = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/wood_wall_window_001_Clean.asset");
            var cleanDoor = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/wood_wall_enter_002_Clean.asset");
            var cleanWall009 = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/wood_wall_009_Clean.asset");
            var cleanWall003 = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/CleanModular/wood_wall_003_Clean.asset");

            if (cleanPlatform == null)
            {
                EditorUtility.DisplayDialog("Notice", "Please click 'Generate Trimmed Clean Meshes' first.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Trimmed Meshes");
            int group = Undo.GetCurrentGroup();

            int applied = 0;
            var mfs = buildingRoot.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                string mName = mf.sharedMesh.name;

                Mesh targetClean = null;
                if (mName == "platform" && cleanPlatform != null) targetClean = cleanPlatform;
                else if (mName == "platform.005" && cleanPlatform005 != null) targetClean = cleanPlatform005;
                else if (mName == "wood_wall.021" && cleanWall != null) targetClean = cleanWall;
                else if (mName == "wood_wall_window.001" && cleanWindow != null) targetClean = cleanWindow;
                else if (mName == "wood_wall_enter.002" && cleanDoor != null) targetClean = cleanDoor;
                else if (mName == "wood_wall.009" && cleanWall009 != null) targetClean = cleanWall009;
                else if (mName == "wood_wall.003" && cleanWall003 != null) targetClean = cleanWall003;

                if (targetClean != null)
                {
                    Undo.RecordObject(mf, "Apply Trimmed Mesh");
                    mf.sharedMesh = targetClean;
                    applied++;
                }
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[ModularSanitizer] Applied trimmed clean meshes to {applied} scene instances.");
        }

        // =========================================================================
        // TAB 4: COLLIDER REBUILDER
        // =========================================================================
        private void DrawColliderRebuilderTab()
        {
            EditorGUILayout.LabelField("Collider Adjustments & Lip Removal", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Replaces jagged, multi-polygon MeshColliders with clean, flush BoxColliders matching visual modular boundaries. Eliminates collision lips that catch players or snag NPC navigation agents.", MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Current Colliders: {totalMeshColliders} MeshColliders / {totalBoxColliders} BoxColliders");

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.9f);
            if (GUILayout.Button("Rebuild Floor Colliders (Flat Continuous Walking Surface)", GUILayout.Height(34)))
            {
                RebuildFloorColliders();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Rebuild Wall & Doorway Colliders (Zero Snagging Lips)", GUILayout.Height(34)))
            {
                RebuildWallColliders();
            }
            GUI.backgroundColor = Color.white;
        }

        private void RebuildFloorColliders()
        {
            if (buildingRoot == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Floor Colliders");
            int group = Undo.GetCurrentGroup();

            int count = 0;
            var floorParents = new string[] { "First Floor/Floor", "Second Floor/Floor" };
            foreach (var pPath in floorParents)
            {
                var parent = buildingRoot.Find(pPath);
                if (parent == null) continue;

                for (int i = 0; i < parent.childCount; i++)
                {
                    var tile = parent.GetChild(i);
                    // Remove existing MeshCollider
                    var mc = tile.GetComponent<MeshCollider>();
                    if (mc != null)
                    {
                        Undo.DestroyObjectImmediate(mc);
                    }

                    // Add or configure BoxCollider
                    var bc = tile.GetComponent<BoxCollider>();
                    if (bc == null)
                    {
                        bc = Undo.AddComponent<BoxCollider>(tile.gameObject);
                    }
                    else
                    {
                        Undo.RecordObject(bc, "Configure BoxCollider");
                    }

                    // Platform: 6m wide x 4m deep x 0.2m thick. Flush top at Y = 0.
                    bc.size = new Vector3(6.0f, 0.20f, 4.0f);
                    bc.center = new Vector3(0f, -0.10f, 2.0f);
                    count++;
                }
            }

            Undo.CollapseUndoOperations(group);
            RunAudit();
            Debug.Log($"[ModularSanitizer] Successfully rebuilt clean BoxColliders on {count} floor platforms.");
        }

        private void RebuildWallColliders()
        {
            if (buildingRoot == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Wall Colliders");
            int group = Undo.GetCurrentGroup();

            int count = 0;
            var wallParents = new string[] { "First Floor/Walls", "Second Floor/Walls" };
            foreach (var pPath in wallParents)
            {
                var parent = buildingRoot.Find(pPath);
                if (parent == null) continue;

                for (int i = 0; i < parent.childCount; i++)
                {
                    var wall = parent.GetChild(i);
                    bool isDoorway = wall.name.Contains("enter") || wall.name.Contains("door");

                    // Remove existing MeshCollider
                    var mc = wall.GetComponent<MeshCollider>();
                    if (mc != null)
                    {
                        Undo.DestroyObjectImmediate(mc);
                    }

                    if (isDoorway)
                    {
                        // Doorway: Left jamb, right jamb, lintel (door aperture remains completely open)
                        var existingBoxes = wall.GetComponents<BoxCollider>();
                        foreach (var b in existingBoxes) Undo.DestroyObjectImmediate(b);

                        var leftJamb = Undo.AddComponent<BoxCollider>(wall.gameObject);
                        leftJamb.size = new Vector3(1.8f, 4.0f, 0.40f);
                        leftJamb.center = new Vector3(-2.1f, 2.0f, 0f);

                        var rightJamb = Undo.AddComponent<BoxCollider>(wall.gameObject);
                        rightJamb.size = new Vector3(1.8f, 4.0f, 0.40f);
                        rightJamb.center = new Vector3(2.1f, 2.0f, 0f);

                        var lintel = Undo.AddComponent<BoxCollider>(wall.gameObject);
                        lintel.size = new Vector3(2.4f, 1.2f, 0.40f);
                        lintel.center = new Vector3(0f, 3.4f, 0f);
                    }
                    else
                    {
                        // Standard solid wall: 6.0m wide x 4.0m tall x 0.40m thick
                        var bc = wall.GetComponent<BoxCollider>();
                        if (bc == null) bc = Undo.AddComponent<BoxCollider>(wall.gameObject);
                        else Undo.RecordObject(bc, "Configure Wall BoxCollider");

                        bc.size = new Vector3(6.0f, 4.0f, 0.40f);
                        bc.center = new Vector3(0f, 2.0f, 0f);
                    }

                    count++;
                }
            }

            Undo.CollapseUndoOperations(group);
            RunAudit();
            Debug.Log($"[ModularSanitizer] Successfully rebuilt non-snagging BoxColliders on {count} wall segments.");
        }

        // =========================================================================
        // TAB 5: NORMAL SMOOTHER
        // =========================================================================
        private void DrawNormalSmootherTab()
        {
            EditorGUILayout.LabelField("Seam Normal Smoothing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Averages normal vectors between adjacent modular meshes sharing coincident boundary vertices. Eliminates dark seams and lighting breaks when directional and baked lighting is calculated.", MessageType.Info);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Recalculate & Smooth Boundary Seam Normals", GUILayout.Height(34)))
            {
                SmoothSeamNormals();
            }
        }

        private void SmoothSeamNormals()
        {
            if (buildingRoot == null) return;

            var mfs = buildingRoot.GetComponentsInChildren<MeshFilter>(true)
                .Where(mf => mf.sharedMesh != null && mf.gameObject.activeInHierarchy).ToArray();

            Debug.Log($"[ModularSanitizer] Smoothing seam normals across {mfs.Length} modular meshes...");

            // Spatial bucket of boundary vertices in world space
            float tolerance = 0.015f;
            var worldVertexBuckets = new Dictionary<Vector3Int, List<(Mesh mesh, int vertexIndex)>>();

            Vector3Int ToKey(Vector3 worldPos)
            {
                return new Vector3Int(
                    Mathf.RoundToInt(worldPos.x / tolerance),
                    Mathf.RoundToInt(worldPos.y / tolerance),
                    Mathf.RoundToInt(worldPos.z / tolerance)
                );
            }

            foreach (var mf in mfs)
            {
                var mesh = mf.sharedMesh;
                var verts = mesh.vertices;
                var trans = mf.transform;

                for (int v = 0; v < verts.Length; v++)
                {
                    // Only boundary vertices
                    if (Mathf.Abs(verts[v].x - (-3f)) < 0.05f || Mathf.Abs(verts[v].x - 3f) < 0.05f)
                    {
                        Vector3 worldPos = trans.TransformPoint(verts[v]);
                        var key = ToKey(worldPos);
                        if (!worldVertexBuckets.TryGetValue(key, out var list))
                        {
                            list = new List<(Mesh, int)>();
                            worldVertexBuckets[key] = list;
                        }
                        list.Add((mesh, v));
                    }
                }
            }

            // Cache mesh normals to prevent allocating new arrays repeatedly
            var meshNormalsCache = new Dictionary<Mesh, Vector3[]>();
            Vector3[] GetNormals(Mesh m)
            {
                if (!meshNormalsCache.TryGetValue(m, out var arr))
                {
                    arr = m.normals;
                    meshNormalsCache[m] = arr;
                }
                return arr;
            }

            int smoothedCount = 0;
            foreach (var pair in worldVertexBuckets)
            {
                if (pair.Value.Count > 1)
                {
                    Vector3 avgNormal = Vector3.zero;
                    foreach (var item in pair.Value)
                    {
                        var normals = GetNormals(item.mesh);
                        avgNormal += normals[item.vertexIndex];
                    }
                    avgNormal.Normalize();

                    foreach (var item in pair.Value)
                    {
                        var normals = GetNormals(item.mesh);
                        normals[item.vertexIndex] = avgNormal;
                    }
                    smoothedCount++;
                }
            }

            foreach (var kvp in meshNormalsCache)
            {
                kvp.Key.normals = kvp.Value;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Normal Smoothing Complete", $"Successfully smoothed seam normals across {smoothedCount} boundary vertex junctions.", "OK");
        }
    }

    public static class EditorGUILayoutExtensions
    {
        public static void LabelOfLarge(string text)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            EditorGUILayout.LabelField(text, style);
        }
    }
}
