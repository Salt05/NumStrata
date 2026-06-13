using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using NumStrata.Data;
using NumStrata.UI;

namespace NumStrata.Gameplay
{
    [System.Serializable]
    public class LevelData
    {
        public string levelName;

        // Legacy (v1): a single shared token array
        public List<string> array;

        // v2: split into multiple arrays; each array represents 1 or more calculations
        public List<TokenArrayData> arrays;
        public int mysteryCount;

        // Structure
        public List<LayerData> layers;
    }

    [System.Serializable]
    public class TokenArrayData
    {
        public List<string> array;
    }

    [System.Serializable]
    public class LayerData
    {
        public int layerIndex;
        public List<string> spawnCoordinates; // List of coordinates like "1-1", "2-3"
    }

    public class LevelLoader : MonoBehaviour
    {
        public static LevelLoader Instance { get; private set; }
        public static bool IsLevelActive { get; set; }

        [Header("Campaign Info")]
        [Tooltip("The unique ID of the current level. If empty, uses file name.")]
        public string levelId;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Auto-instantiate GameplayUIManager if missing
            if (UnityEngine.Object.FindFirstObjectByType<NumStrata.UI.GameplayUIManager>() == null)
            {
                var go = new GameObject("GameplayUIManager");
                go.AddComponent<NumStrata.UI.GameplayUIManager>();
            }
        }

        [Header("Prefabs")]
        [Tooltip("The prefab for Board_Grid")]
        public GameObject boardGridPrefab;
        [Tooltip("The prefab for Tile_Base")]
        public GameObject tileBasePrefab;

        [Header("References")]
        [Tooltip("The parent for all layers (Img_BoardBackground)")]
        public Transform boardBackground;

        [Header("Data Source")]
        [Tooltip("Tùy chọn: gán sẵn trong scene để test. Nếu trống, load từ Resources/Campaign/{levelId}.")]
        public TextAsset levelDataJsonFile;
        [Tooltip("Level mặc định khi không có PendingLevelId (tên file không .json).")]
        public string fallbackLevelId = "campaign_0001";
        public TileSpriteData tileSpriteData;

        private List<string> pool;
        public List<Equation> ParsedEquations => parsedEquations;
        private List<Equation> parsedEquations;
        public List<Tile> allSpawnedTiles = new List<Tile>();
        private Dictionary<Tile, int> tileDebugIds = new Dictionary<Tile, int>();

        [Header("Generation Settings")]
        [Tooltip("Luong Operator toi da mo khoa cung luc de tranh deadlock")]
        public int maxOpenOperators = 2;
        [Tooltip("Print detailed spawn and win-plan logs in the Unity Console.")]
        public bool enableSpawnPlanDebugLog = true;

        // Hệ số trọng số cho công thức Sorting Order: op = (Row * RowWeight) + (Layer * LayerWeight)
        private const int RowWeight = 1000;
        private const int LayerWeight = 100;

        private void Start()
        {
            TextAsset levelFile = ResolveLevelFile();
            if (levelFile != null)
            {
                levelDataJsonFile = levelFile;
                LoadLevel(levelFile.text);
            }
        }

        private TextAsset ResolveLevelFile()
        {
            string pendingId = PlayerPrefs.GetString(CampaignSession.PendingLevelIdKey, string.Empty);
            if (!string.IsNullOrEmpty(pendingId))
            {
                PlayerPrefs.DeleteKey(CampaignSession.PendingLevelIdKey);
                PlayerPrefs.Save();

                TextAsset campaignFile = FindCampaignLevelFile(pendingId);
                if (campaignFile != null)
                {
                    levelId = pendingId;
                    return campaignFile;
                }

                Debug.LogWarning($"[LevelLoader] Không tìm thấy JSON cho '{pendingId}', dùng level mặc định trong scene.");
            }

            if (levelDataJsonFile != null)
            {
                if (string.IsNullOrEmpty(levelId))
                {
                    levelId = levelDataJsonFile.name;
                }
                return levelDataJsonFile;
            }

            TextAsset fallback = LoadFallbackCampaignLevel();
            if (fallback != null)
            {
                levelId = fallbackLevelId;
            }

            return fallback;
        }

        private TextAsset FindCampaignLevelFile(string targetLevelId)
        {
            if (string.IsNullOrEmpty(targetLevelId))
            {
                return null;
            }

            string resourcePath = CampaignSession.GetCampaignResourcePath(targetLevelId);
            TextAsset file = Resources.Load<TextAsset>(resourcePath);
            if (file == null)
            {
                bool isChallenge = PlayerPrefs.GetInt("IsChallengeMode", 0) == 1;
                string expectedFolder = isChallenge ? "Streak" : "Campaign";
                Debug.LogWarning($"[LevelLoader] Resources.Load không tìm thấy '{resourcePath}'. Đặt file tại Assets/_Game/Resources/{expectedFolder}/{targetLevelId}.json");
            }

            return file;
        }

        private TextAsset LoadFallbackCampaignLevel()
        {
            if (string.IsNullOrEmpty(fallbackLevelId))
            {
                return null;
            }

            return FindCampaignLevelFile(fallbackLevelId);
        }

        [ContextMenu("Test Load Level")]
        public void TestLoad()
        {
            if (levelDataJsonFile != null)
            {
                LoadLevel(levelDataJsonFile.text);
            }
            else
            {
                Debug.LogError("[LevelLoader] JSON file is not assigned.");
            }
        }

        public void LoadLevel(string levelJson)
        {
            // 1. Find Img_BoardBackground if not assigned
            if (boardBackground == null)
            {
                GameObject bg = GameObject.Find("Img_BoardBackground");
                if (bg != null) boardBackground = bg.transform;
            }

            if (boardBackground == null)
            {
                Debug.LogError("[LevelLoader] boardBackground (Img_BoardBackground) not found. Please assign it or check object name.");
                return;
            }

            // 2. Clear existing layers in Img_BoardBackground
            foreach (Transform child in boardBackground)
            {
                SafeDestroy(child.gameObject);
            }

            // 3. Parse JSON
            LevelData levelData = JsonUtility.FromJson<LevelData>(levelJson);

            if (levelData == null)
            {
                Debug.LogError("[LevelLoader] Failed to parse level JSON file.");
                return;
            }

            if (!string.IsNullOrEmpty(levelData.levelName))
            {
                levelId = levelData.levelName;
            }

            // 4. Build pool + equation plan from level data
            pool = BuildPoolAndEquations(levelData, out parsedEquations);
            allSpawnedTiles.Clear();
            tileDebugIds.Clear();

            Debug.Log($"[LevelLoader] Loading level with {levelData.layers.Count} layers. Pool size: {pool.Count}");


            // 5. Spawn layers (Khởi tạo vỏ Tile trước, chưa gán giá trị)
            foreach (var layerData in levelData.layers)
            {
                SpawnLayerStructure(layerData);
            }

            // 6. Generate Overlap Tree (Xây dựng cây đè để biết Tile nào mở, Tile nào khóa)
            GenerateOverlapTree();

            // KIỂM TRA SESSION RESUME
            SessionResume resume = null;
            if (LocalDataManager.Instance != null && LocalDataManager.Instance.CurrentPlayer != null && LocalDataManager.Instance.CurrentPlayer.campaign.hasActiveRun)
            {
                resume = LocalDataManager.Instance.LoadSessionResume();
            }

            if (resume != null && resume.activeLevelId == GetCurrentLevelId())
            {
                RestoreSession(resume);

                // Cập nhật lại UI đếm số lượng Tile sau khi Load xong
                if (TileCounter.Instance != null) TileCounter.Instance.UpdateTileCountUI();

                Debug.Log("[LevelLoader] Level load completed.");

                IsLevelActive = true;
                if (HelperManager.Instance != null)
                {
                    HelperManager.Instance.SetLevelHelperUses(resume.levelHelperUses);
                }
                if (LocalDataManager.Instance != null)
                {
                    LocalDataManager.Instance.BeginCampaignRun(GetCurrentLevelId());
                }
            }
            else
            {
                IsLevelActive = false;
                StartCoroutine(SmartPopulateValuesRoutine(levelData.mysteryCount));
            }
        }

        public string GetCurrentLevelId()
        {
            if (!string.IsNullOrEmpty(levelId))
            {
                return levelId;
            }
            if (levelDataJsonFile != null)
            {
                return levelDataJsonFile.name;
            }
            return "Level_Test";
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                SaveSessionResumeNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveSessionResumeNow();
        }

        public void SaveSessionResumeNow()
        {
            if (!IsLevelActive || LocalDataManager.Instance == null) return;

            SessionResume resume = new SessionResume();
            resume.activeLevelId = GetCurrentLevelId();
            if (HelperManager.Instance != null)
            {
                resume.levelHelperUses = HelperManager.Instance.GetLevelHelperUses();
            }

            // Save state of all tiles currently active in gameplay
            List<Tile> allTiles = Tile.AllExistingTiles;
            foreach (Tile tile in allTiles)
            {
                if (tile == null || !tile.gameObject.activeInHierarchy || !tile.isAssigned) continue;
                if (tile.type == TileType.Helper) continue; // skip helpers in popup grid

                TileSaveState tileState = new TileSaveState();
                tileState.gridX = tile.gridX;
                tileState.gridY = tile.gridY;
                tileState.layerId = tile.layerId;
                tileState.tileType = tile.type.ToString();
                tileState.numberValue = tile.numberValue;
                tileState.operatorValue = tile.operatorValue;
                tileState.isMystery = tile.isMystery;

                // Determine location
                if (FormulaManager.Instance != null)
                {
                    int formulaIdx = System.Array.IndexOf(FormulaManager.Instance.occupiedTiles, tile);
                    int conveyorIdx = System.Array.IndexOf(FormulaManager.Instance.occupiedConveyorTiles, tile);

                    if (formulaIdx >= 0)
                    {
                        tileState.location = "formula";
                        tileState.slotIndex = formulaIdx;
                    }
                    else if (conveyorIdx >= 0)
                    {
                        tileState.location = "conveyor";
                        tileState.slotIndex = conveyorIdx;
                    }
                    else
                    {
                        tileState.location = "board";
                    }
                }
                else
                {
                    tileState.location = "board";
                }

                resume.tiles.Add(tileState);
            }

            LocalDataManager.Instance.SaveSessionResume(resume);
        }

        private void RestoreSession(SessionResume resume)
        {
            Debug.Log($"[LevelLoader] Restoring session with {resume.tiles.Count} tiles.");

            // 1. Dựng map các tile theo toạ độ để lookup
            var tileMap = new Dictionary<string, Tile>();
            foreach (Tile t in allSpawnedTiles)
            {
                string key = $"{t.layerId}_{t.gridX}_{t.gridY}";
                tileMap[key] = t;
            }

            List<Tile> matchedTiles = new List<Tile>();

            foreach (TileSaveState tSave in resume.tiles)
            {
                string key = $"{tSave.layerId}_{tSave.gridX}_{tSave.gridY}";
                if (tileMap.TryGetValue(key, out Tile existingTile))
                {
                    // Update value
                    TileType type;
                    if (!System.Enum.TryParse(tSave.tileType, out type))
                    {
                        type = TileType.Number;
                    }

                    if (type == TileType.Number)
                    {
                        existingTile.SetupNumber(tSave.numberValue, tileSpriteData.GetNumberSprite(tSave.numberValue));
                    }
                    else if (type == TileType.Operator)
                    {
                        existingTile.SetupOperator(tSave.operatorValue, tileSpriteData.GetOperatorSprite(tSave.operatorValue));
                    }

                    if (tSave.isMystery)
                        existingTile.SetMysteryMask(true);

                    // Re-locate
                    if (tSave.location == "formula" && FormulaManager.Instance != null)
                    {
                        if (tSave.slotIndex >= 0 && tSave.slotIndex < FormulaManager.Instance.occupiedTiles.Length)
                        {
                            FormulaManager.Instance.occupiedTiles[tSave.slotIndex] = existingTile;
                            MoveTileToSlotImmediate(existingTile, FormulaManager.Instance.formulaSlots[tSave.slotIndex]);
                        }
                    }
                    else if (tSave.location == "conveyor" && FormulaManager.Instance != null)
                    {
                        if (tSave.slotIndex >= 0 && tSave.slotIndex < FormulaManager.Instance.occupiedConveyorTiles.Length)
                        {
                            FormulaManager.Instance.occupiedConveyorTiles[tSave.slotIndex] = existingTile;
                            MoveTileToSlotImmediate(existingTile, FormulaManager.Instance.conveyorSlots[tSave.slotIndex]);
                        }
                    }

                    matchedTiles.Add(existingTile);
                }
            }

            // Xoá những tile không có trong file save
            for (int i = allSpawnedTiles.Count - 1; i >= 0; i--)
            {
                Tile t = allSpawnedTiles[i];
                if (!matchedTiles.Contains(t))
                {
                    RemoveTileFromOverlapTree(t);
                    if (t != null && t.gameObject != null)
                    {
                        Destroy(t.gameObject);
                    }
                    allSpawnedTiles.RemoveAt(i);
                }
            }
        }

        private void RemoveTileFromOverlapTree(Tile tile)
        {
            if (tile == null) return;
            foreach (Tile coveringTile in tile.coveringTiles)
            {
                if (coveringTile != null) coveringTile.coveredTiles.Remove(tile);
            }
            foreach (Tile coveredTile in tile.coveredTiles)
            {
                if (coveredTile != null)
                {
                    coveredTile.coveringTiles.Remove(tile);
                    coveredTile.SetVisualState(coveredTile.coveringTiles.Count == 0);
                }
            }
            tile.coveringTiles.Clear();
            tile.coveredTiles.Clear();
        }

        private void MoveTileToSlotImmediate(Tile tile, Transform slotTransform)
        {
            RectTransform rectT = tile.GetComponent<RectTransform>();
            if (rectT == null) return;

            tile.transform.SetParent(slotTransform, true); 

            UnityEngine.UI.AspectRatioFitter aspectFitter = tile.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            if (aspectFitter != null) aspectFitter.enabled = false;

            rectT.anchorMin = new Vector2(0.5f, 0.5f);
            rectT.anchorMax = new Vector2(0.5f, 0.5f);
            rectT.pivot = new Vector2(0.5f, 0.5f);
            rectT.anchoredPosition = Vector2.zero;
            rectT.SetAsLastSibling();

            RemoveTileFromOverlapTree(tile);

            UnityEngine.UI.Button btn = tile.GetComponent<UnityEngine.UI.Button>();
            if (btn != null) btn.interactable = false;

            // Đồng bộ kích thước luôn nếu có FormulaManager
            if (FormulaManager.Instance != null && FormulaManager.Instance.referenceBoardSlot != null)
            {
                NumStrata.UI.UISizeSync sizeSync = tile.GetComponent<NumStrata.UI.UISizeSync>();
                if (sizeSync == null) sizeSync = tile.gameObject.AddComponent<NumStrata.UI.UISizeSync>();
                sizeSync.target = FormulaManager.Instance.referenceBoardSlot;
                sizeSync.syncWidth = true;
                sizeSync.syncHeight = true;
            }
        }

        private void ApplyMysteryMasks(int count)
        {
            if (count <= 0) return;

            List<Tile> candidates = new List<Tile>();
            foreach (Tile t in allSpawnedTiles)
            {
                if (t.isAssigned && t.type != TileType.Helper && !t.isMystery)
                {
                    candidates.Add(t);
                }
            }

            System.Random rnd = new System.Random();
            for (int i = 0; i < candidates.Count; i++)
            {
                int r = rnd.Next(i, candidates.Count);
                Tile tmp = candidates[i];
                candidates[i] = candidates[r];
                candidates[r] = tmp;
            }

            int applied = 0;
            for (int i = 0; i < candidates.Count && i < count; i++)
            {
                candidates[i].SetMysteryMask(true);
                applied++;
            }
            Debug.Log($"[LevelLoader] Đã áp dụng {applied} mặt nạ Mystery.");
        }

        private void GenerateOverlapTree()
        {
            // For every spawned tile, check against every other tile
            // tileB overlaps tileA if tileB.layer > tileA.layer AND coordinate distance is <= 1
            for (int i = 0; i < allSpawnedTiles.Count; i++)
            {
                Tile tileA = allSpawnedTiles[i];

                for (int j = 0; j < allSpawnedTiles.Count; j++)
                {
                    if (i == j) continue;
                    Tile tileB = allSpawnedTiles[j];

                    // tileB (above) covers tileA (below)
                    if (tileB.layerId > tileA.layerId)
                    {
                        if (Mathf.Abs(tileA.gridX - tileB.gridX) <= 1 && Mathf.Abs(tileA.gridY - tileB.gridY) <= 1)
                        {
                            // It's an overlap!
                            tileB.AddCoveredTile(tileA);
                            tileA.AddCoveringTile(tileB);
                        }
                    }
                }
            }

            // Debug tree structure
            foreach (var tile in allSpawnedTiles)
            {
                // Gọi SetVisualState để ép màu cho toàn bộ Tile: Sáng nếu không bị đè, tối nếu bị đè.
                tile.SetVisualState(tile.coveringTiles.Count == 0);
            }
            
            Debug.Log($"[LevelLoader] Overlap Tree generated for {allSpawnedTiles.Count} tiles.");
            if (enableSpawnPlanDebugLog)
            {
                Debug.Log(BuildSpawnTreeReport());
            }
        }

        private System.Collections.IEnumerator SmartPopulateValuesRoutine(int mysteryCount)
        {
            if (GameplayUIManager.Instance != null)
            {
                GameplayUIManager.Instance.ShowLoadingWheel(true);
            }

            if (pool == null || pool.Count == 0)
            {
                if (GameplayUIManager.Instance != null) GameplayUIManager.Instance.ShowLoadingWheel(false);
                yield break;
            }

            List<Equation> equations = parsedEquations ?? new List<Equation>();
            if (equations.Count == 0)
            {
                Debug.LogWarning("[LevelLoader] No equations parsed from pool; falling back to random fill.");
                FallbackRandomFill(pool);
                if (GameplayUIManager.Instance != null) GameplayUIManager.Instance.ShowLoadingWheel(false);
                ApplyMysteryMasks(mysteryCount);
                if (TileCounter.Instance != null) TileCounter.Instance.UpdateTileCountUI();
                IsLevelActive = true;
                if (HelperManager.Instance != null) HelperManager.Instance.ResetHelperUses();
                if (LocalDataManager.Instance != null) LocalDataManager.Instance.BeginCampaignRun(GetCurrentLevelId());
                yield break;
            }

            System.Random rnd = new System.Random();
            Dictionary<int, Tile> tileById = BuildTileByIdMap();

            // Convert to thread-safe data container
            Dictionary<int, SolverTileData> solverTileDataMap = new Dictionary<int, SolverTileData>();
            foreach (var kvp in tileById)
            {
                Tile t = kvp.Value;
                SolverTileData std = new SolverTileData
                {
                    id = kvp.Key,
                    gridX = t.gridX,
                    gridY = t.gridY
                };
                solverTileDataMap[kvp.Key] = std;
            }

            foreach (var kvp in tileById)
            {
                Tile t = kvp.Value;
                SolverTileData std = solverTileDataMap[kvp.Key];
                foreach (var coveringTile in t.coveringTiles)
                {
                    if (coveringTile != null)
                    {
                        int covId = GetTileDebugId(coveringTile);
                        if (covId > 0)
                        {
                            std.coveringTileIds.Add(covId);
                        }
                    }
                }
            }

            // Run solver on background thread
            System.Threading.Tasks.Task<SolverResult> solverTask = System.Threading.Tasks.Task.Run(() =>
            {
                SolverResult res;
                bool solved = LevelSolver.TrySolveSpawnAndWinningPlan(equations, solverTileDataMap, rnd, out res);
                return solved ? res : null;
            });

            // Wait for task completion asynchronously
            while (!solverTask.IsCompleted)
            {
                yield return null;
            }

            if (GameplayUIManager.Instance != null)
            {
                GameplayUIManager.Instance.ShowLoadingWheel(false);
            }

            SolverResult solverResult = solverTask.Result;

            if (solverResult == null)
            {
                Debug.LogWarning("[LevelLoader] Could not find a non-deadlock winning plan. Falling back to random fill.");
                FallbackRandomFill(pool);
            }
            else
            {
                foreach (var step in solverResult.spawnSteps)
                {
                    if (tileById.TryGetValue(step.tileId, out Tile tile))
                    {
                        ApplyValueToTile(tile, step.token);
                    }
                }

                if (enableSpawnPlanDebugLog)
                {
                    List<string> lines = new List<string>();
                    int stepIndex = 1;
                    foreach (var step in solverResult.winningPlanSteps)
                    {
                        lines.Add($"Step {stepIndex}: {step.token} [{step.coord}]");
                        stepIndex++;
                    }
                    Debug.Log("[SpawnPlan]\n" + string.Join("\n", lines));
                }

                List<Tile> remaining = new List<Tile>();
                foreach (var t in allSpawnedTiles) if (!t.isAssigned) remaining.Add(t);
                List<string> leftovers = new List<string>();
                foreach (var s in pool) leftovers.Add(s);
                foreach (var eq in solverResult.equationOrder)
                {
                    foreach (var tok in eq.GetTokens()) leftovers.Remove(tok);
                }
                int li = 0;
                while (li < leftovers.Count && remaining.Count > 0)
                {
                    ApplyValueToTile(remaining[0], leftovers[li]);
                    remaining.RemoveAt(0);
                    li++;
                }
            }

            // Apply mystery masks
            ApplyMysteryMasks(mysteryCount);

            // Update Tile Counter UI
            if (TileCounter.Instance != null) TileCounter.Instance.UpdateTileCountUI();

            Debug.Log("[LevelLoader] Level load completed.");

            // Gameplay State hooks
            IsLevelActive = true;
            if (HelperManager.Instance != null)
            {
                HelperManager.Instance.ResetHelperUses();
            }
            if (LocalDataManager.Instance != null)
            {
                LocalDataManager.Instance.BeginCampaignRun(GetCurrentLevelId());
            }
        }

        private Dictionary<int, Tile> BuildTileByIdMap()
        {
            Dictionary<int, Tile> map = new Dictionary<int, Tile>();
            foreach (var tile in allSpawnedTiles)
            {
                int id = GetTileDebugId(tile);
                if (id > 0) map[id] = tile;
            }
            return map;
        }

        private void FallbackRandomFill(List<string> poolData)
        {
            List<Tile> emptyTiles = new List<Tile>();
            foreach (var t in allSpawnedTiles) if (!t.isAssigned) emptyTiles.Add(t);
            List<string> items = new List<string>(poolData);
            while (items.Count > 0 && emptyTiles.Count > 0)
            {
                int ri = Random.Range(0, emptyTiles.Count);
                ApplyValueToTile(emptyTiles[ri], items[0]);
                items.RemoveAt(0);
                emptyTiles.RemoveAt(ri);
            }
        }
        private List<Equation> ParseEquations(List<string> tokens)
        {
            List<Equation> eqs = new List<Equation>();
            int i = 0;
            while (i < tokens.Count)
            {
                // Pattern: num, op, num, resultTokens...
                // resultTokens must match calculated result split into tile digits (e.g. 56 -> "5","6")
                if (i + 3 < tokens.Count &&
                    int.TryParse(tokens[i], out int n1) &&
                    !int.TryParse(tokens[i + 1], out _) &&
                    int.TryParse(tokens[i + 2], out int n2))
                {
                    string op = tokens[i + 1];
                    int expected = Calculate(n1, op, n2);
                    if (expected != -999999)
                    {
                        List<string> expectedResultTokens = SplitResultIntoTiles(expected);
                        if (MatchTokenSlice(tokens, i + 3, expectedResultTokens))
                        {
                            List<string> sourceTokens = new List<string>();
                            sourceTokens.Add(tokens[i]);
                            sourceTokens.Add(op);
                            sourceTokens.Add(tokens[i + 2]);
                            sourceTokens.AddRange(expectedResultTokens);
                            eqs.Add(new Equation(n1, op, n2, expected, sourceTokens, sourceTokens));
                            i += sourceTokens.Count;
                            continue;
                        }

                        // Backward compatibility: allow a legacy single-token result (e.g. "56"),
                        // but always spawn as split tiles ("5","6").
                        if (int.TryParse(tokens[i + 3], out int rawResult) && rawResult == expected)
                        {
                            List<string> sourceTokens = new List<string>
                            {
                                tokens[i],
                                op,
                                tokens[i + 2],
                                tokens[i + 3]
                            };

                            List<string> placementTokens = new List<string>
                            {
                                tokens[i],
                                op,
                                tokens[i + 2]
                            };
                            placementTokens.AddRange(expectedResultTokens);

                            eqs.Add(new Equation(n1, op, n2, expected, sourceTokens, placementTokens));
                            i += sourceTokens.Count;
                            continue;
                        }
                    }
                }
                i++;
            }
            return eqs;
        }

        private List<string> BuildPoolAndEquations(LevelData levelData, out List<Equation> equations)
        {
            equations = new List<Equation>();
            List<string> builtPool = new List<string>();
            if (levelData == null)
            {
                return builtPool;
            }

            // Collect arrays (v2 preferred, v1 fallback)
            List<List<string>> groups = new List<List<string>>();
            if (levelData.arrays != null && levelData.arrays.Count > 0)
            {
                foreach (var g in levelData.arrays)
                {
                    if (g?.array == null || g.array.Count == 0) continue;
                    groups.Add(new List<string>(g.array));
                }
            }
            else if (levelData.array != null && levelData.array.Count > 0)
            {
                groups.Add(new List<string>(levelData.array));
            }

            // Parse each group as a chain of calculations, producing placement tokens (including remainder tokens).
            foreach (var rawGroup in groups)
            {
                if (rawGroup == null || rawGroup.Count == 0) continue;
                if (TryParseEquationGroup(rawGroup, out Equation eq, out List<string> placementTokens))
                {
                    equations.Add(eq);
                    builtPool.AddRange(placementTokens);
                }
                else
                {
                    // If group cannot be parsed, fall back: treat tokens as plain pool tokens.
                    builtPool.AddRange(rawGroup);
                }
            }

            return builtPool;
        }

        private bool TryParseEquationGroup(List<string> groupTokens, out Equation equation, out List<string> placementTokens)
        {
            equation = null;
            placementTokens = new List<string>();
            if (groupTokens == null || groupTokens.Count == 0) return false;

            List<string> sourceTokens = new List<string>(groupTokens);

            int? carry = null; // used when a remainder is generated (division with remainder) and next calc is like "+,1,2"
            int i = 0;
            int calcCount = 0;

            while (i < groupTokens.Count)
            {
                string t = groupTokens[i]?.Trim();
                if (string.IsNullOrEmpty(t))
                {
                    i++;
                    continue;
                }

                // Case A: operator-leading calc: op, b, result (a comes from carry)
                if (IsOperatorToken(t))
                {
                    if (carry == null) return false;
                    if (i + 2 >= groupTokens.Count) return false;
                    if (!int.TryParse(groupTokens[i + 1], out int b)) return false;
                    if (!int.TryParse(groupTokens[i + 2], out int rawResult)) return false;

                    int a = carry.Value;
                    string op = NormalizeOperatorToken(t);
                    AppendCalculationTokens(a, op, b, rawResult, placementTokens, ref carry);
                    i += 3;
                    calcCount++;
                    continue;
                }

                // Case B: number-leading calc (support both v1 infix: a,op,b,result and v2 postfix: a,b,op,result)
                if (!int.TryParse(t, out int n1)) return false;

                // Case B0: carry-leading alternate form: b, op, result (a comes from carry)
                // Example: after a division with remainder, chain might be "1,+,2" instead of "+,1,2"
                if (carry != null &&
                    i + 2 < groupTokens.Count &&
                    IsOperatorToken(groupTokens[i + 1]) &&
                    int.TryParse(groupTokens[i + 2], out int r_carry_form))
                {
                    int a = carry.Value;
                    int b = n1;
                    string op = NormalizeOperatorToken(groupTokens[i + 1]);
                    AppendCalculationTokens(a, op, b, r_carry_form, placementTokens, ref carry);
                    i += 3;
                    calcCount++;
                    continue;
                }

                // Prefer postfix: a,b,op,result (ex: "1,1,+,2" / "3,2,/,1")
                if (i + 3 < groupTokens.Count &&
                    int.TryParse(groupTokens[i + 1], out int n2_post) &&
                    IsOperatorToken(groupTokens[i + 2]) &&
                    int.TryParse(groupTokens[i + 3], out int r_post))
                {
                    string op = NormalizeOperatorToken(groupTokens[i + 2]);
                    AppendCalculationTokens(n1, op, n2_post, r_post, placementTokens, ref carry);
                    i += 4;
                    calcCount++;
                    continue;
                }

                // Fallback infix: a,op,b,result (legacy)
                if (i + 3 < groupTokens.Count &&
                    IsOperatorToken(groupTokens[i + 1]) &&
                    int.TryParse(groupTokens[i + 2], out int n2) &&
                    int.TryParse(groupTokens[i + 3], out int r))
                {
                    string op = NormalizeOperatorToken(groupTokens[i + 1]);
                    AppendCalculationTokens(n1, op, n2, r, placementTokens, ref carry);
                    i += 4;
                    calcCount++;
                    continue;
                }

                return false;
            }

            if (calcCount <= 0) return false;

            // Equation object is used mainly as a token container for solver.
            equation = new Equation(0, "group", 0, 0, sourceTokens, placementTokens);
            return true;
        }

        private void AppendCalculationTokens(int a, string op, int b, int rawResult, List<string> outputPlacementTokens, ref int? carry)
        {
            // Normalize token order to match old behavior: a, op, b, resultTokens...
            outputPlacementTokens.Add(a.ToString());
            outputPlacementTokens.Add(op);
            outputPlacementTokens.Add(b.ToString());

            List<string> resultTokens = SplitResultIntoTiles(rawResult);
            outputPlacementTokens.AddRange(resultTokens);

            // Division with remainder: auto-generate remainder tile(s) and set carry for next operator-leading calc.
            if (op == "/" && b != 0)
            {
                int remainder = a % b;
                if (remainder != 0)
                {
                    List<string> remainderTokens = SplitResultIntoTiles(remainder);
                    outputPlacementTokens.AddRange(remainderTokens);
                    carry = remainder;
                    return;
                }
            }

            carry = null;
        }

        private bool IsOperatorToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            string t = token.Trim();
            return t == "+" || t == "-" || t == "x" || t == "*" || t == "/" || t == ":"; // ":" legacy division
        }

        private string NormalizeOperatorToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return token;
            string t = token.Trim();
            if (t == "*") return "x";
            if (t == ":") return "/";
            return t;
        }
        private bool MatchTokenSlice(List<string> source, int startIndex, List<string> expected)
        {
            if (source == null || expected == null) return false;
            if (startIndex < 0 || startIndex + expected.Count > source.Count) return false;
            for (int i = 0; i < expected.Count; i++)
            {
                if (source[startIndex + i] != expected[i]) return false;
            }
            return true;
        }
        private List<string> SplitResultIntoTiles(int value)
        {
            List<string> resultTokens = new List<string>();
            string absValue = Mathf.Abs(value).ToString();
            for (int i = 0; i < absValue.Length; i++)
            {
                string digit = absValue[i].ToString();
                if (i == 0 && value < 0)
                {
                    resultTokens.Add("-" + digit);
                }
                else
                {
                    resultTokens.Add(digit);
                }
            }
            return resultTokens;
        }
        private int Calculate(int a, string op, int b)
        {
            switch (op)
            {
                case "+": return a + b;
                case "-": return a - b;
                case "x": case "*": return a * b;
                case "/": case ":": return b != 0 ? a / b : 0;
                default: return -999999;
            }
        }

        public class Equation
        {
            public int a;
            public string op;
            public int b;
            public int result;
            public List<string> sourceTokens;
            public List<string> placementTokens;
            public Equation(int a, string op, int b, int r, List<string> sourceTokens, List<string> placementTokens)
            {
                this.a = a;
                this.op = op;
                this.b = b;
                this.result = r;
                this.sourceTokens = sourceTokens != null ? new List<string>(sourceTokens) : new List<string>();
                this.placementTokens = placementTokens != null ? new List<string>(placementTokens) : new List<string>();
            }
            public List<string> GetTokens()
            {
                return new List<string>(placementTokens);
            }
            public List<string> GetSourceTokens()
            {
                return new List<string>(sourceTokens);
            }
            public override string ToString() => $"{a}{op}{b}={result}";
        }
        private void ApplyValueToTile(Tile tile, string value)
        {
            tile.gameObject.name = $"Tile_{value}_{tile.layerId}_{tile.gridX}-{tile.gridY}";

            if (int.TryParse(value, out int num))
                tile.SetupNumber(num, tileSpriteData.GetNumberSprite(num));
            else
            {
                string op = value == "*" ? "x" : value;
                tile.SetupOperator(op, tileSpriteData.GetOperatorSprite(op));
            }
        }

        private void SpawnLayerStructure(LayerData layerData)
        {
            GameObject layerObj = Instantiate(boardGridPrefab, boardBackground);
            layerObj.name = $"Board_Grid_Layer_{layerData.layerIndex}";
            layerObj.transform.SetAsLastSibling(); 

            // Nhóm các tọa độ theo dòng (row)
            // Giả sử tọa độ có định dạng "X-Y", ta sẽ lấy `X` làm Row
            Dictionary<int, List<string>> rowsDict = new Dictionary<int, List<string>>();
            foreach (string coord in layerData.spawnCoordinates)
            {
                string[] parts = coord.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out int row))
                {
                    if (!rowsDict.ContainsKey(row))
                        rowsDict[row] = new List<string>();
                    rowsDict[row].Add(coord);
                }
            }

            // Sắp xếp các dòng theo thứ tự tăng dần
            List<int> sortedRows = new List<int>(rowsDict.Keys);
            sortedRows.Sort();

            foreach (int row in sortedRows)
            {
                foreach (string coord in rowsDict[row])
                {
                    string slotName = $"Slot_{coord}";
                    Transform slot = FindRecursive(layerObj.transform, slotName);

                    if (slot != null)
                    {
                        SpawnTileEmpty(slot, layerData.layerIndex, coord);
                    }
                }
            }
        }

        private void SpawnTileEmpty(Transform slot, int layerId, string coord)
        {
            GameObject tileObj = Instantiate(tileBasePrefab, slot);
            
            RectTransform rect = tileObj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            Tile tile = tileObj.GetComponent<Tile>();
            tile.layerId = layerId;
            string[] coordParts = coord.Split('-');
            if (coordParts.Length == 2 && int.TryParse(coordParts[0], out int x) && int.TryParse(coordParts[1], out int y))
            {
                tile.gridX = x;
                tile.gridY = y;
            }

            // Gán Sorting Order theo công thức: op = (Row * RowWeight) + (Layer * LayerWeight)
            Canvas canvas = tileObj.GetComponent<Canvas>();
            if (canvas == null) canvas = tileObj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = (tile.gridX * RowWeight) + (layerId * LayerWeight);

            UnityEngine.UI.GraphicRaycaster raycaster = tileObj.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null) tileObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            tileDebugIds[tile] = allSpawnedTiles.Count + 1;
            allSpawnedTiles.Add(tile);
        }

        public List<Tile> GetActiveBoardTiles()
        {
            List<Tile> activeTiles = new List<Tile>();
            foreach (Tile tile in allSpawnedTiles)
            {
                if (tile != null && tile.transform.IsChildOf(boardBackground))
                {
                    activeTiles.Add(tile);
                }
            }
            return activeTiles;
        }

        private string BuildSpawnTreeReport()
        {
            List<Tile> sorted = new List<Tile>(allSpawnedTiles);
            sorted.Sort((a, b) => GetTileDebugId(a).CompareTo(GetTileDebugId(b)));

            List<string> lines = new List<string>();
            lines.Add("[SpawnTree]");
            lines.Add("Tile positions:");
            foreach (var tile in sorted)
            {
                int id = GetTileDebugId(tile);
                lines.Add($"#{id}: [{tile.gridX}-{tile.gridY}] L{tile.layerId}");
            }

            lines.Add("Cover relations:");
            List<string> independent = new List<string>();
            foreach (var tile in sorted)
            {
                int id = GetTileDebugId(tile);
                if (tile.coveredTiles.Count > 0)
                {
                    List<int> coveredIds = new List<int>();
                    foreach (var covered in tile.coveredTiles)
                    {
                        if (covered != null) coveredIds.Add(GetTileDebugId(covered));
                    }
                    coveredIds.Sort();
                    lines.Add($"#{id} de #{string.Join(",#", coveredIds)}");
                }
                else if (tile.coveringTiles.Count == 0)
                {
                    independent.Add($"#{id}");
                }
            }

            if (independent.Count > 0)
            {
                lines.Add($"{string.Join(",", independent)} doc lap (khong de ai)");
            }

            return string.Join("\n", lines);
        }

        private int GetTileDebugId(Tile tile)
        {
            if (tile == null) return -1;
            if (tileDebugIds.TryGetValue(tile, out int id)) return id;

            id = allSpawnedTiles.IndexOf(tile) + 1;
            if (id < 1) id = 0;
            tileDebugIds[tile] = id;
            return id;
        }

        #region Utilities

        private Transform FindRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void SafeDestroy(GameObject obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        #endregion
    }
    }







