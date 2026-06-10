using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using NumStrata.Gameplay;
using System.Linq;

namespace NumStrata.Editor
{
    public class LevelEditorWindow : EditorWindow
    {
        private LevelData levelData;
        private int currentLayerIndex = 0;
        private Vector2 scrollPos;
        private int selectedTab = 0;
        private readonly string[] tabs = { "Board Layout", "Equations & Constraints" };

        [MenuItem("NumStrata/Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(500, 600);
            window.InitializeData();
        }

        private void InitializeData()
        {
            if (levelData == null)
            {
                levelData = new LevelData
                {
                    levelName = "Level_",
                    arrays = new List<TokenArrayData>(),
                    layers = new List<LayerData> { new LayerData { layerIndex = 0, spawnCoordinates = new List<string>() } },
                    mysteryCount = 0
                };
            }
        }

        private void OnGUI()
        {
            InitializeData();

            DrawToolbar();

            EditorGUILayout.Space();
            levelData.levelName = EditorGUILayout.TextField("Level Name", levelData.levelName);
            EditorGUILayout.Space();

            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            if (selectedTab == 0)
            {
                DrawBoardLayoutTab();
            }
            else
            {
                DrawEquationsTab();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("New Level", EditorStyles.toolbarButton))
            {
                if (EditorUtility.DisplayDialog("New Level", "Are you sure? Unsaved changes will be lost.", "Yes", "No"))
                {
                    levelData = null;
                    currentLayerIndex = 0;
                    InitializeData();
                }
            }

            if (GUILayout.Button("Load JSON", EditorStyles.toolbarButton))
            {
                string path = EditorUtility.OpenFilePanel("Load Level JSON", Application.dataPath + "/_Game/Data", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    string json = File.ReadAllText(path);
                    levelData = JsonUtility.FromJson<LevelData>(json);

                    // Ensure Lists are instantiated
                    if (levelData.layers == null) levelData.layers = new List<LayerData>();
                    if (levelData.layers.Count == 0) levelData.layers.Add(new LayerData { layerIndex = 0, spawnCoordinates = new List<string>() });
                    if (levelData.arrays == null) levelData.arrays = new List<TokenArrayData>();

                    currentLayerIndex = 0;
                }
            }

            if (GUILayout.Button("Save JSON", EditorStyles.toolbarButton))
            {
                int totalTiles = levelData.layers.Sum(l => l.spawnCoordinates != null ? l.spawnCoordinates.Count : 0);
                int totalTokens = levelData.arrays.Sum(a => a.array != null ? a.array.Count : 0);

                List<string> invalidTokens = new List<string>();
                foreach (var arrayData in levelData.arrays)
                {
                    if (arrayData.array == null) continue;
                    foreach (var token in arrayData.array)
                    {
                        if (int.TryParse(token, out int num))
                        {
                            if (num < -9 || num > 9) invalidTokens.Add(token);
                        }
                        else
                        {
                            if (token != "+" && token != "-" && token != "x" && token != "/") invalidTokens.Add(token);
                        }
                    }
                }

                if (invalidTokens.Count > 0)
                {
                    string invalidList = string.Join(", ", invalidTokens.Distinct());
                    EditorUtility.DisplayDialog("Validation Error", $"Cannot save!\n\nFound invalid tokens: {invalidList}\n\nValid tokens are numbers from -9 to 9, and operators +, -, x, /.", "OK");
                }
                else if (totalTiles != totalTokens)
                {
                    EditorUtility.DisplayDialog("Validation Error", $"Cannot save!\n\nTotal Slots on Board: {totalTiles}\nTotal Tokens in Equations: {totalTokens}\n\nThey must be equal.", "OK");
                }
                else
                {
                    string defaultFileName = string.IsNullOrEmpty(levelData.levelName) ? "Level_" : levelData.levelName;
                    string path = EditorUtility.SaveFilePanel("Save Level JSON", Application.dataPath + "/_Game/Data", defaultFileName, "json");
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Clean up and sort structure
                        foreach (var layer in levelData.layers)
                        {
                            if (layer.spawnCoordinates != null)
                            {
                                layer.spawnCoordinates = layer.spawnCoordinates.Distinct().ToList();
                                // Sort coordinates by Row, then Col logic if needed
                            }
                        }

                        string json = JsonUtility.ToJson(levelData, true);
                        File.WriteAllText(path, json);
                        AssetDatabase.Refresh();
                        Debug.Log($"[LevelEditor] Saved Level JSON to: {path}");
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawBoardLayoutTab()
        {
            EditorGUILayout.LabelField("Layer Management", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("<", GUILayout.Width(50)) && currentLayerIndex > 0)
            {
                currentLayerIndex--;
            }

            EditorGUILayout.LabelField($"Layer: {currentLayerIndex}", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(100));

            if (GUILayout.Button(">", GUILayout.Width(50)))
            {
                currentLayerIndex++;
                EnsureLayerExists(currentLayerIndex);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Delete Current Layer", GUILayout.Width(150)))
            {
                if (levelData.layers.Count > 1)
                {
                    levelData.layers.RemoveAt(currentLayerIndex);
                    // Update index sequentially
                    for (int i = 0; i < levelData.layers.Count; i++) levelData.layers[i].layerIndex = i;
                    if (currentLayerIndex >= levelData.layers.Count) currentLayerIndex = levelData.layers.Count - 1;
                }
            }

            GUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            LayerData activeLayer = levelData.layers[currentLayerIndex];
            if (activeLayer.spawnCoordinates == null) activeLayer.spawnCoordinates = new List<string>();

            EditorGUILayout.LabelField("11x11 Spawn Grid (Row-Col)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Click to toggle a Tile spawn point on the current layer.", MessageType.Info);

            GUILayout.BeginVertical("box");
            
            // Draw visual grid X, Y (1 to 11)
            for (int y = 1; y <= 11; y++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                for (int x = 1; x <= 11; x++)
                {
                    string coord = $"{x}-{y}";
                    bool isActive = activeLayer.spawnCoordinates.Contains(coord);
                    
                    // Display current state
                    GUI.backgroundColor = isActive ? Color.green : Color.white;
                    if (GUILayout.Button(isActive ? "X" : "", GUILayout.Width(30), GUILayout.Height(30)))
                    {
                        if (isActive)
                            activeLayer.spawnCoordinates.Remove(coord);
                        else
                            activeLayer.spawnCoordinates.Add(coord);
                    }
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Total Tiles in this layer: {activeLayer.spawnCoordinates.Count}");
            
            int totalTiles = levelData.layers.Sum(l => l.spawnCoordinates != null ? l.spawnCoordinates.Count : 0);
            EditorGUILayout.LabelField($"Total Tiles in all layers (S): <color=yellow><b>{totalTiles}</b></color>", new GUIStyle(EditorStyles.label) { richText = true });
        }

        private void DrawEquationsTab()
        {
            EditorGUILayout.LabelField("Equations (Tile Inventory Pool)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Each array represents a chain of calculation. Example: [1, 2, +, 3]", MessageType.Info);

            if (GUILayout.Button("Add New Equation Array"))
            {
                levelData.arrays.Add(new TokenArrayData { array = new List<string>() });
            }

            for (int i = 0; i < levelData.arrays.Count; i++)
            {
                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Equation Chain {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    levelData.arrays.RemoveAt(i);
                    break;
                }
                GUILayout.EndHorizontal();

                TokenArrayData arrayData = levelData.arrays[i];
                if (arrayData.array == null) arrayData.array = new List<string>();

                string joinedStr = string.Join(",", arrayData.array);
                string newJoinedStr = EditorGUILayout.TextField("Tokens (comma separated)", joinedStr);
                
                if (newJoinedStr != joinedStr)
                {
                    arrayData.array = newJoinedStr.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                }

                GUILayout.EndVertical();
            }

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Game Constraints", EditorStyles.boldLabel);
            levelData.mysteryCount = EditorGUILayout.IntField("Mystery Tiles Count", levelData.mysteryCount);

            EditorGUILayout.Space(10);
            int totalTokens = levelData.arrays.Sum(a => a.array != null ? a.array.Count : 0);
            EditorGUILayout.LabelField($"Total Tokens in all equations: <color=cyan><b>{totalTokens}</b></color>", new GUIStyle(EditorStyles.label) { richText = true });
        }

        private void EnsureLayerExists(int index)
        {
            while (levelData.layers.Count <= index)
            {
                levelData.layers.Add(new LayerData
                {
                    layerIndex = levelData.layers.Count,
                    spawnCoordinates = new List<string>()
                });
            }
        }
    }
}