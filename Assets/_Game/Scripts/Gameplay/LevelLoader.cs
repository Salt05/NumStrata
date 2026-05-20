using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using NumStrata.Data;

namespace NumStrata.Gameplay
{
    [System.Serializable]
    public class TileInventoryData
    {
        // Legacy (v1): a single shared token array
        public List<string> array;

        // v2: split into multiple arrays; each array represents 1 or more calculations
        public List<TokenArrayData> arrays;
        public int mysteryCount;
    }

    [System.Serializable]
    public class TokenArrayData
    {
        public List<string> array;
    }

    [System.Serializable]
    public class LevelStructureData
    {
        public List<LayerData> layers;
    }

    [System.Serializable]
    public class LayerData
    {
        public int layerIndex;
        public List<string> spawnCoordinates; // List of coordinates like "1-1", "2-3"
    }

    public class LevelLoader : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("The prefab for Board_Grid")]
        public GameObject boardGridPrefab;
        [Tooltip("The prefab for Tile_Base")]
        public GameObject tileBasePrefab;

        [Header("References")]
        [Tooltip("The parent for all layers (Img_BoardBackground)")]
        public Transform boardBackground;

        [Header("Data Source")]
        public TextAsset inventoryJsonFile;
        public TextAsset structureJsonFile;
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

        private void Start()
        {
            if (inventoryJsonFile != null && structureJsonFile != null)
            {
                LoadLevel(inventoryJsonFile.text, structureJsonFile.text);
            }
        }

        [ContextMenu("Test Load Level")]
        public void TestLoad()
        {
            if (inventoryJsonFile != null && structureJsonFile != null)
            {
                LoadLevel(inventoryJsonFile.text, structureJsonFile.text);
            }
            else
            {
                Debug.LogError("[LevelLoader] JSON files are not assigned.");
            }
        }

        public void LoadLevel(string inventoryJson, string structureJson)
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

            // 3. Parse JSONs
            TileInventoryData inventory = JsonUtility.FromJson<TileInventoryData>(inventoryJson);
            LevelStructureData structure = JsonUtility.FromJson<LevelStructureData>(structureJson);

            if (inventory == null || structure == null)
            {
                Debug.LogError("[LevelLoader] Failed to parse level JSON files.");
                return;
            }

            // 4. Build pool + equation plan from inventory data
            pool = BuildPoolAndEquations(inventory, out parsedEquations);
            allSpawnedTiles.Clear();
            tileDebugIds.Clear();

            Debug.Log($"[LevelLoader] Loading level with {structure.layers.Count} layers. Pool size: {pool.Count}");

            int globalSortingOrder = 1;

            // 5. Spawn layers (Khởi tạo vỏ Tile trước, chưa gán giá trị)
            foreach (var layerData in structure.layers)
            {
                SpawnLayerStructure(layerData, ref globalSortingOrder);
            }

            // 6. Generate Overlap Tree (Xây dựng cây đè để biết Tile nào mở, Tile nào khóa)
            GenerateOverlapTree();

            // 7. Smart Populate Values (Rải giá trị từ Pool vào Cây để tránh Deadlock)
            SmartPopulateValues();

            // 8. Rải mặt nạ Mystery
            ApplyMysteryMasks(inventory.mysteryCount);

            // Cập nhật lại UI đếm số lượng Tile sau khi Load xong
            if (TileCounter.Instance != null) TileCounter.Instance.UpdateTileCountUI();

            Debug.Log("[LevelLoader] Level load completed.");
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

        private void SmartPopulateValues()
        {
            if (pool == null || pool.Count == 0) return;
            List<Equation> equations = parsedEquations ?? new List<Equation>();
            if (equations.Count == 0)
            {
                Debug.LogWarning("[LevelLoader] No equations parsed from pool; falling back to random fill.");
                FallbackRandomFill(pool);
                return;
            }
            System.Random rnd = new System.Random();
            if (!TrySolveSpawnAndWinningPlan(equations, rnd, out SolverResult solverResult))
            {
                Debug.LogWarning("[LevelLoader] Could not find a non-deadlock winning plan. Falling back to random fill.");
                FallbackRandomFill(pool);
                return;
            }
            Dictionary<int, Tile> tileById = BuildTileByIdMap();
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
        private class SolverStep
        {
            public string token;
            public int tileId;
            public string coord;
            public SolverStep(string token, int tileId, string coord)
            {
                this.token = token;
                this.tileId = tileId;
                this.coord = coord;
            }
        }
        private class SolverResult
        {
            public List<SolverStep> spawnSteps;
            public List<SolverStep> winningPlanSteps;
            public List<Equation> equationOrder;
        }
        private bool TrySolveSpawnAndWinningPlan(List<Equation> equations, System.Random rnd, out SolverResult result)
        {
            result = null;
            Dictionary<int, Tile> tileById = BuildTileByIdMap();
            if (tileById.Count == 0) return false;
            const int maxAttempts = 300;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                List<Equation> equationOrder = new List<Equation>(equations);
                for (int i = 0; i < equationOrder.Count; i++)
                {
                    int j = rnd.Next(i, equationOrder.Count);
                    Equation tmp = equationOrder[i];
                    equationOrder[i] = equationOrder[j];
                    equationOrder[j] = tmp;
                }
                if (TrySolveForEquationOrder(equationOrder, tileById, rnd, out SolverResult solved))
                {
                    result = solved;
                    return true;
                }
            }
            return false;
        }
        private bool TrySolveForEquationOrder(List<Equation> equationOrder, Dictionary<int, Tile> tileById, System.Random rnd, out SolverResult result)
        {
            result = null;
            SolverResult solvedResult = null;
            HashSet<int> removed = new HashSet<int>();
            HashSet<int> unassigned = new HashSet<int>(tileById.Keys);
            List<List<SolverStep>> segments = new List<List<SolverStep>>();
            bool SearchEquation(int equationIndex)
            {
                if (equationIndex >= equationOrder.Count)
                {
                    List<int> finalPlanIds = BuildPlanIdsFromSegments(segments, null);
                    if (!IsReplayUnlockable(finalPlanIds, tileById)) return false;
                    List<SolverStep> spawnSteps = new List<SolverStep>();
                    List<SolverStep> winningSteps = new List<SolverStep>();
                    foreach (var seg in segments)
                    {
                        spawnSteps.AddRange(seg);
                        for (int i = seg.Count - 1; i >= 0; i--)
                        {
                            winningSteps.Add(seg[i]);
                        }
                    }
                    solvedResult = new SolverResult
                    {
                        spawnSteps = spawnSteps,
                        winningPlanSteps = winningSteps,
                        equationOrder = new List<Equation>(equationOrder)
                    };
                    return true;
                }
                List<string> eqTokens = equationOrder[equationIndex].GetTokens();
                eqTokens.Reverse(); // spawn order: result -> operands
                List<SolverStep> currentSegment = new List<SolverStep>();
                bool SearchToken(int tokenIndex)
                {
                    if (tokenIndex >= eqTokens.Count)
                    {
                        segments.Add(new List<SolverStep>(currentSegment));
                        if (SearchEquation(equationIndex + 1)) return true;
                        segments.RemoveAt(segments.Count - 1);
                        return false;
                    }
                    List<int> candidates = CollectUnlockedUnassigned(unassigned, removed, tileById);
                    if (candidates.Count == 0) return false;
                    ShuffleInPlace(candidates, rnd);
                    string token = eqTokens[tokenIndex];
                    foreach (int candidateId in candidates)
                    {
                        Tile tile = tileById[candidateId];
                        string coord = $"{tile.gridX}-{tile.gridY}";
                        removed.Add(candidateId);
                        unassigned.Remove(candidateId);
                        currentSegment.Add(new SolverStep(token, candidateId, coord));
                        List<int> partialPlanIds = BuildPlanIdsFromSegments(segments, currentSegment);
                        bool feasiblePrefix = IsReplayUnlockable(partialPlanIds, tileById);
                        if (feasiblePrefix && SearchToken(tokenIndex + 1)) return true;
                        currentSegment.RemoveAt(currentSegment.Count - 1);
                        unassigned.Add(candidateId);
                        removed.Remove(candidateId);
                    }
                    return false;
                }
                return SearchToken(0);
            }
            bool solved = SearchEquation(0);
            if (solved) result = solvedResult;
            return solved;
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
        private List<int> CollectUnlockedUnassigned(HashSet<int> unassigned, HashSet<int> removed, Dictionary<int, Tile> tileById)
        {
            List<int> candidates = new List<int>();
            foreach (int id in unassigned)
            {
                if (IsUnlockedByIds(id, removed, tileById)) candidates.Add(id);
            }
            return candidates;
        }
        private bool IsUnlockedByIds(int tileId, HashSet<int> removed, Dictionary<int, Tile> tileById)
        {
            if (!tileById.TryGetValue(tileId, out Tile tile) || tile == null) return false;
            foreach (var coveringTile in tile.coveringTiles)
            {
                if (coveringTile == null) continue;
                int coveringId = GetTileDebugId(coveringTile);
                if (coveringId > 0 && !removed.Contains(coveringId)) return false;
            }
            return true;
        }
        private List<int> BuildPlanIdsFromSegments(List<List<SolverStep>> committedSegments, List<SolverStep> currentSegment)
        {
            List<int> ids = new List<int>();
            foreach (var seg in committedSegments)
            {
                for (int i = seg.Count - 1; i >= 0; i--)
                {
                    ids.Add(seg[i].tileId);
                }
            }
            if (currentSegment != null)
            {
                for (int i = currentSegment.Count - 1; i >= 0; i--)
                {
                    ids.Add(currentSegment[i].tileId);
                }
            }
            return ids;
        }
        private bool IsReplayUnlockable(List<int> planTileIds, Dictionary<int, Tile> tileById)
        {
            HashSet<int> replayRemoved = new HashSet<int>();
            foreach (int id in planTileIds)
            {
                if (!IsUnlockedByIds(id, replayRemoved, tileById)) return false;
                replayRemoved.Add(id);
            }
            return true;
        }
        private void ShuffleInPlace(List<int> values, System.Random rnd)
        {
            for (int i = 0; i < values.Count; i++)
            {
                int j = rnd.Next(i, values.Count);
                int tmp = values[i];
                values[i] = values[j];
                values[j] = tmp;
            }
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

        private List<string> BuildPoolAndEquations(TileInventoryData inventory, out List<Equation> equations)
        {
            equations = new List<Equation>();
            List<string> builtPool = new List<string>();
            if (inventory == null)
            {
                return builtPool;
            }

            // Collect arrays (v2 preferred, v1 fallback)
            List<List<string>> groups = new List<List<string>>();
            if (inventory.arrays != null && inventory.arrays.Count > 0)
            {
                foreach (var g in inventory.arrays)
                {
                    if (g?.array == null || g.array.Count == 0) continue;
                    groups.Add(new List<string>(g.array));
                }
            }
            else if (inventory.array != null && inventory.array.Count > 0)
            {
                groups.Add(new List<string>(inventory.array));
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

        private void SpawnLayerStructure(LayerData layerData, ref int globalSortingOrder)
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
                        SpawnTileEmpty(slot, layerData.layerIndex, coord, globalSortingOrder);
                    }
                }
                // Tăng globalSortingOrder sau khi spawn xong 1 dòng
                globalSortingOrder++;
            }
        }

        private void SpawnTileEmpty(Transform slot, int layerId, string coord, int sortingOrder)
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

            // Gán Sorting Order
            Canvas canvas = tileObj.GetComponent<Canvas>();
            if (canvas == null) canvas = tileObj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

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







