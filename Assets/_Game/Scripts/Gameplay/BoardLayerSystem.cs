using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEngine.UI;
using NumStrata.Data;

namespace NumStrata.Gameplay
{
    /// <summary>
    /// Context: Chuyển đổi sang hệ thống Board 11x11 dựa trên tọa độ và Layer.
    /// Task for Copilot: Triển khai logic nạp dữ liệu từ JSON và spawn tile theo layer.
    /// Constraints: 
    /// - Spawn từ Layer thấp đến Layer cao.
    /// - Tile không làm con trực tiếp của Slot (giữ size cố định).
    /// - Sử dụng ma trận Board_Grid 11x11 làm điểm neo (Anchor).
    /// </summary>
    public class BoardLayerSystem : MonoBehaviour
    {
        [Serializable]
        private class LayeredLayoutData
        {
            public string layoutId;
            public string gridSize;
            public List<LayerData> layers;
        }

        [Serializable]
        private class LayerData
        {
            public int layerIndex;
            public List<string> spawnCoordinates;
        }

        [Serializable]
        private class TileValueData
        {
            public List<string> tileValues;
            public int mysteryCount;
        }

        private sealed class SpawnedTileEntry
        {
            public string coordinateKey;
            public int layerIndex;
            public Tile tile;
        }

        [Header("References")]
        [SerializeField] private RectTransform boardGrid; // Prefab/template Board_Grid
        [SerializeField] private RectTransform boardBackgroundParent; // Img_BoardBackground
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileSpriteData tileSpriteData;

        [Header("Data Source")]
        [SerializeField] private string layoutJsonPath = "Assets/_Game/Data/Layouts/layout_test_layered.json";
        [SerializeField] private string tileValueJsonPath = "Assets/_Game/Data/Layouts/tile_values_test.json";

        [Header("Runtime")]
        [SerializeField] private bool autoBuildOnStart = true;

        // Layer -> (coordinateKey -> slot transform)
        private readonly Dictionary<int, Dictionary<string, RectTransform>> layerSlotAnchors = new Dictionary<int, Dictionary<string, RectTransform>>();
        private readonly Dictionary<int, RectTransform> layerRoots = new Dictionary<int, RectTransform>();
        private readonly List<SpawnedTileEntry> spawnedTiles = new List<SpawnedTileEntry>();
        private readonly Dictionary<string, List<SpawnedTileEntry>> tileStacksByCoordinate = new Dictionary<string, List<SpawnedTileEntry>>();

        private LayeredLayoutData loadedLayout;
        private TileValueData loadedTileValues;

        private void Start()
        {
            if (!autoBuildOnStart)
            {
                return;
            }

            BuildBoardFromConfiguredData();
        }

        [ContextMenu("Build Board From Configured Data")]
        public void BuildBoardFromConfiguredData()
        {
            LoadTileValues(tileValueJsonPath);
            LoadLayout(layoutJsonPath);
            SpawnTilesByLayer();
        }

        /// <summary>
        /// Khởi tạo ma trận điểm neo cho Layer 0 từ Board_Grid hiện có trong scene.
        /// Dùng khi cần map nhanh cấu trúc hiện tại trước khi spawn.
        /// </summary>
        public void InitializeSlotMatrix()
        {
            if (boardGrid == null)
            {
                Debug.LogError("[BoardLayerSystem] boardGrid is not assigned.", this);
                return;
            }

            Dictionary<string, RectTransform> anchors = CollectAnchorsFromGrid(boardGrid);
            layerSlotAnchors[0] = anchors;
            Debug.Log($"[BoardLayerSystem] Initialized layer-0 slot matrix. Anchors={anchors.Count}", this);
        }

        /// <summary>
        /// Nạp dữ liệu pool giá trị Tile (numbers/operators/mysteryCount)
        /// </summary>
        public void LoadTileValues(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                Debug.LogError("[BoardLayerSystem] tile value jsonPath is empty.", this);
                return;
            }

            string jsonContent = TryGetJsonContent(jsonPath);
            if (string.IsNullOrEmpty(jsonContent))
            {
                Debug.LogError($"[BoardLayerSystem] Cannot load tile value JSON from '{jsonPath}'.", this);
                return;
            }

            TileValueData parsedData;
            try
            {
                parsedData = JsonUtility.FromJson<TileValueData>(jsonContent);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoardLayerSystem] Tile value JSON parse failed for '{jsonPath}'. Error: {ex.Message}", this);
                return;
            }

            if (parsedData == null)
            {
                Debug.LogError($"[BoardLayerSystem] Tile value JSON is invalid for '{jsonPath}'.", this);
                return;
            }

            parsedData.tileValues ??= new List<string>();
            if (parsedData.mysteryCount < 0)
            {
                Debug.LogWarning("[BoardLayerSystem] mysteryCount < 0. Clamped to 0.", this);
                parsedData.mysteryCount = 0;
            }

            loadedTileValues = parsedData;
            Debug.Log($"[BoardLayerSystem] Loaded tile values. Count={loadedTileValues.tileValues.Count}, mysteryCount={loadedTileValues.mysteryCount}", this);
        }

        /// <summary>
        /// Nạp dữ liệu layout từ file JSON (v2 schema)
        /// </summary>
        public void LoadLayout(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                Debug.LogError("[BoardLayerSystem] jsonPath is empty.", this);
                return;
            }

            string jsonContent = TryGetJsonContent(jsonPath);
            if (string.IsNullOrEmpty(jsonContent))
            {
                Debug.LogError($"[BoardLayerSystem] Cannot load JSON content from '{jsonPath}'.", this);
                return;
            }

            LayeredLayoutData parsedData;
            try
            {
                parsedData = JsonUtility.FromJson<LayeredLayoutData>(jsonContent);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoardLayerSystem] JSON parse failed for '{jsonPath}'. Error: {ex.Message}", this);
                return;
            }

            if (parsedData == null || parsedData.layers == null)
            {
                Debug.LogError($"[BoardLayerSystem] Parsed layout is invalid for '{jsonPath}'.", this);
                return;
            }

            loadedLayout = parsedData;
            Debug.Log($"[BoardLayerSystem] Loaded layout '{loadedLayout.layoutId}' with {loadedLayout.layers.Count} layer(s).", this);
        }

        /// <summary>
        /// Thực hiện spawn Tile theo từng layer
        /// Thứ tự: Layer 0 -> Layer N
        /// </summary>
        public void SpawnTilesByLayer()
        {
            if (loadedLayout == null || loadedLayout.layers == null)
            {
                Debug.LogError("[BoardLayerSystem] Layout data is empty. Call LoadLayout before SpawnTilesByLayer.", this);
                return;
            }

            if (tilePrefab == null)
            {
                Debug.LogError("[BoardLayerSystem] tilePrefab is not assigned.", this);
                return;
            }

            if (boardGrid == null)
            {
                Debug.LogError("[BoardLayerSystem] boardGrid prefab/template is not assigned.", this);
                return;
            }

            if (boardBackgroundParent == null)
            {
                Debug.LogError("[BoardLayerSystem] boardBackgroundParent (Img_BoardBackground) is not assigned.", this);
                return;
            }

            if (loadedTileValues == null)
            {
                Debug.LogError("[BoardLayerSystem] Tile value data is empty. Call LoadTileValues before SpawnTilesByLayer.", this);
                return;
            }

            List<LayerData> sortedLayers = loadedLayout.layers.OrderBy(layer => layer.layerIndex).ToList();
            int availableSlots = CountAvailableSlots(sortedLayers);
            List<string> drawPool = BuildDrawPool();

            if (drawPool.Count > availableSlots)
            {
                Debug.LogError($"[BoardLayerSystem] Tile pool ({drawPool.Count}) exceeds slot capacity ({availableSlots}). Adjust JSON data.", this);
                return;
            }

            ClearRuntimeObjects();
            BuildLayerRootsAndAnchors(sortedLayers);

            RectTransform prefabRect = tilePrefab.GetComponent<RectTransform>();
            int spawnedCount = 0;

            foreach (LayerData layer in sortedLayers)
            {
                if (drawPool.Count == 0)
                {
                    break;
                }

                if (layer == null || layer.spawnCoordinates == null || layer.spawnCoordinates.Count == 0)
                {
                    continue;
                }

                if (!layerSlotAnchors.TryGetValue(layer.layerIndex, out Dictionary<string, RectTransform> anchors))
                {
                    Debug.LogWarning($"[BoardLayerSystem] Missing anchor matrix for layer {layer.layerIndex}.", this);
                    continue;
                }

                foreach (string rawCoordinate in layer.spawnCoordinates)
                {
                    if (drawPool.Count == 0)
                    {
                        break;
                    }

                    if (!TryNormalizeCoordinateKey(rawCoordinate, out string coordinateKey))
                    {
                        Debug.LogWarning($"[BoardLayerSystem] Invalid coordinate '{rawCoordinate}' in layer {layer.layerIndex}.", this);
                        continue;
                    }

                    if (!anchors.TryGetValue(coordinateKey, out RectTransform slotTransform))
                    {
                        Debug.LogWarning($"[BoardLayerSystem] Missing slot '{coordinateKey}' in layer {layer.layerIndex}.", this);
                        continue;
                    }

                    string token = DrawRandomToken(drawPool);
                    if (!SpawnTileIntoSlot(layer.layerIndex, coordinateKey, slotTransform, token, prefabRect))
                    {
                        continue;
                    }

                    spawnedCount++;
                }
            }

            if (drawPool.Count > 0)
            {
                Debug.LogWarning($"[BoardLayerSystem] Remaining {drawPool.Count} tile token(s) were not spawned.", this);
            }

            UpdateTileVisibility();
            Debug.Log($"[BoardLayerSystem] Spawn completed. Spawned={spawnedCount}", this);
        }

        /// <summary>
        /// Cập nhật trạng thái Unlocked cho các Tile
        /// </summary>
        public void UpdateTileVisibility()
        {
            foreach (KeyValuePair<string, List<SpawnedTileEntry>> kvp in tileStacksByCoordinate)
            {
                List<SpawnedTileEntry> stack = kvp.Value;
                if (stack == null || stack.Count == 0)
                {
                    continue;
                }

                stack.RemoveAll(entry => entry == null || entry.tile == null);
                if (stack.Count == 0)
                {
                    continue;
                }

                stack.Sort((a, b) => b.layerIndex.CompareTo(a.layerIndex));

                bool isTopTile = true;
                foreach (SpawnedTileEntry entry in stack)
                {
                    ApplyTileLockState(entry.tile, isTopTile);
                    isTopTile = false;
                }
            }
        }

        private Dictionary<string, RectTransform> CollectAnchorsFromGrid(RectTransform gridRoot)
        {
            Dictionary<string, RectTransform> anchors = new Dictionary<string, RectTransform>();

            BoardCoordinate[] coordinates = gridRoot.GetComponentsInChildren<BoardCoordinate>(true);
            foreach (BoardCoordinate coordinate in coordinates)
            {
                if (coordinate == null)
                {
                    continue;
                }

                RectTransform anchor = coordinate.GetComponent<RectTransform>();
                if (anchor == null)
                {
                    continue;
                }

                if (coordinate.Row < 1 || coordinate.Row > 11 || coordinate.Column < 1 || coordinate.Column > 11)
                {
                    continue;
                }

                string key = coordinate.GetCoordinateKey();
                anchors[key] = anchor;
            }

            return anchors;
        }

        private void BuildLayerRootsAndAnchors(List<LayerData> sortedLayers)
        {
            foreach (LayerData layer in sortedLayers)
            {
                if (layer == null)
                {
                    continue;
                }

                RectTransform layerRoot = Instantiate(boardGrid, boardBackgroundParent);
                layerRoot.name = $"Board_Grid_Layer_{layer.layerIndex}";
                layerRoots[layer.layerIndex] = layerRoot;

                Dictionary<string, RectTransform> anchors = CollectAnchorsFromGrid(layerRoot);
                layerSlotAnchors[layer.layerIndex] = anchors;
            }
        }

        private int CountAvailableSlots(List<LayerData> sortedLayers)
        {
            int total = 0;
            foreach (LayerData layer in sortedLayers)
            {
                if (layer == null || layer.spawnCoordinates == null)
                {
                    continue;
                }

                total += layer.spawnCoordinates.Count;
            }

            return total;
        }

        private List<string> BuildDrawPool()
        {
            List<string> pool = new List<string>();

            if (loadedTileValues.tileValues != null)
            {
                for (int i = 0; i < loadedTileValues.tileValues.Count; i++)
                {
                    string token = loadedTileValues.tileValues[i];
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }

                    pool.Add(token.Trim());
                }
            }

            for (int i = 0; i < loadedTileValues.mysteryCount; i++)
            {
                pool.Add("?");
            }

            return pool;
        }

        private string DrawRandomToken(List<string> drawPool)
        {
            int index = UnityEngine.Random.Range(0, drawPool.Count);
            string value = drawPool[index];
            drawPool.RemoveAt(index);
            return value;
        }

        private bool SpawnTileIntoSlot(int layerIndex, string coordinateKey, RectTransform slotTransform, string token, RectTransform prefabRect)
        {
            GameObject tileObject = Instantiate(tilePrefab, slotTransform);
            tileObject.name = $"Tile_{coordinateKey}_L{layerIndex}";

            RectTransform tileRect = tileObject.GetComponent<RectTransform>();
            if (tileRect != null)
            {
                tileRect.anchoredPosition = Vector2.zero;
                tileRect.localScale = Vector3.one;
                if (prefabRect != null)
                {
                    tileRect.sizeDelta = prefabRect.sizeDelta;
                }
            }

            LayoutElement layoutElement = tileObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = tileObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = true;

            Tile tile = tileObject.GetComponent<Tile>();
            if (tile == null)
            {
                Debug.LogError($"[BoardLayerSystem] tilePrefab instance at '{tileObject.name}' is missing Tile component.", tileObject);
                Destroy(tileObject);
                return false;
            }

            ApplyTokenToTile(tile, token);

            SpawnedTileEntry entry = new SpawnedTileEntry
            {
                coordinateKey = coordinateKey,
                layerIndex = layerIndex,
                tile = tile
            };

            spawnedTiles.Add(entry);

            if (!tileStacksByCoordinate.TryGetValue(coordinateKey, out List<SpawnedTileEntry> stack))
            {
                stack = new List<SpawnedTileEntry>();
                tileStacksByCoordinate[coordinateKey] = stack;
            }

            stack.Add(entry);
            return true;
        }

        private void ApplyTokenToTile(Tile tile, string token)
        {
            if (token == "?")
            {
                Sprite mystery = tileSpriteData != null ? tileSpriteData.mysterySprite : null;
                tile.SetupMystery(mystery);
                return;
            }

            if (int.TryParse(token, out int numberValue))
            {
                Sprite numberSprite = tileSpriteData != null ? tileSpriteData.GetNumberSprite(numberValue) : null;
                tile.SetupNumber(numberValue, numberSprite);
                return;
            }

            string normalizedOperator = NormalizeOperator(token);
            Sprite operatorSprite = tileSpriteData != null ? tileSpriteData.GetOperatorSprite(normalizedOperator) : null;
            tile.SetupOperator(normalizedOperator, operatorSprite);
        }

        private string NormalizeOperator(string token)
        {
            string trimmed = token.Trim();
            if (trimmed == "*")
            {
                return "x";
            }

            return trimmed;
        }

        private void ApplyTileLockState(Tile tile, bool unlocked)
        {
            tile.SetVisualState(unlocked);

            CanvasGroup canvasGroup = tile.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = tile.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.interactable = unlocked;
            canvasGroup.blocksRaycasts = unlocked;
        }

        private void ClearRuntimeObjects()
        {
            for (int i = 0; i < spawnedTiles.Count; i++)
            {
                SpawnedTileEntry entry = spawnedTiles[i];
                if (entry?.tile == null)
                {
                    continue;
                }

                Destroy(entry.tile.gameObject);
            }

            foreach (KeyValuePair<int, RectTransform> layerRoot in layerRoots)
            {
                if (layerRoot.Value != null)
                {
                    Destroy(layerRoot.Value.gameObject);
                }
            }

            spawnedTiles.Clear();
            tileStacksByCoordinate.Clear();
            layerRoots.Clear();
            layerSlotAnchors.Clear();
        }

        private bool TryNormalizeCoordinateKey(string rawCoordinate, out string normalized)
        {
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(rawCoordinate))
            {
                return false;
            }

            string[] parts = rawCoordinate.Trim().Split('-');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out int row) || !int.TryParse(parts[1], out int column))
            {
                return false;
            }

            if (row < 1 || row > 11 || column < 1 || column > 11)
            {
                return false;
            }

            normalized = $"{row}-{column}";
            return true;
        }

        private string TryGetJsonContent(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                return null;
            }

            string trimmed = jsonPath.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                return trimmed;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            List<string> candidatePaths = new List<string>();

            if (Path.IsPathRooted(trimmed))
            {
                candidatePaths.Add(trimmed);
            }
            else
            {
                candidatePaths.Add(trimmed);

                if (!string.IsNullOrEmpty(projectRoot))
                {
                    candidatePaths.Add(Path.Combine(projectRoot, trimmed));
                }

                if (trimmed.StartsWith("Assets/") || trimmed.StartsWith("Assets\\"))
                {
                    string relativeToAssets = trimmed.Substring("Assets/".Length).TrimStart('/', '\\');
                    candidatePaths.Add(Path.Combine(Application.dataPath, relativeToAssets));
                }
                else
                {
                    candidatePaths.Add(Path.Combine(Application.dataPath, trimmed));
                }
            }

            for (int i = 0; i < candidatePaths.Count; i++)
            {
                string candidate = candidatePaths[i];
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    return File.ReadAllText(candidate);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BoardLayerSystem] Failed reading '{candidate}'. Error: {ex.Message}", this);
                    return null;
                }
            }

            Debug.LogError($"[BoardLayerSystem] JSON file not found. Tried {candidatePaths.Count} candidate path(s) from '{jsonPath}'.", this);
            return null;
        }
    }
}
