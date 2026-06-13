using System;
using System.Collections.Generic;
using UnityEngine;

namespace NumStrata.Gameplay
{
    public class SolverStep
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

    public class SolverResult
    {
        public List<SolverStep> spawnSteps;
        public List<SolverStep> winningPlanSteps;
        public List<LevelLoader.Equation> equationOrder;
    }

    /// <summary>
    /// Pure C# container representing a Tile for thread-safe operations in the solver thread.
    /// </summary>
    public class SolverTileData
    {
        public int id;
        public int gridX;
        public int gridY;
        public List<int> coveringTileIds = new List<int>();
    }

    public static class LevelSolver
    {
        /// <summary>
        /// Solves equations and maps values to board tiles. This is thread-safe and can run on a background thread.
        /// </summary>
        public static bool TrySolveSpawnAndWinningPlan(
            List<LevelLoader.Equation> equations, 
            Dictionary<int, SolverTileData> tileById, 
            System.Random rnd, 
            out SolverResult result)
        {
            result = null;
            if (tileById.Count == 0) return false;
            const int maxAttempts = 300;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                List<LevelLoader.Equation> equationOrder = new List<LevelLoader.Equation>(equations);
                for (int i = 0; i < equationOrder.Count; i++)
                {
                    int j = rnd.Next(i, equationOrder.Count);
                    LevelLoader.Equation tmp = equationOrder[i];
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

        private static bool TrySolveForEquationOrder(
            List<LevelLoader.Equation> equationOrder, 
            Dictionary<int, SolverTileData> tileById, 
            System.Random rnd, 
            out SolverResult result)
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
                        equationOrder = new List<LevelLoader.Equation>(equationOrder)
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
                        SolverTileData tile = tileById[candidateId];
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

        private static List<int> CollectUnlockedUnassigned(
            HashSet<int> unassigned, 
            HashSet<int> removed, 
            Dictionary<int, SolverTileData> tileById)
        {
            List<int> candidates = new List<int>();
            foreach (int id in unassigned)
            {
                if (IsUnlockedByIds(id, removed, tileById)) candidates.Add(id);
            }
            return candidates;
        }

        private static bool IsUnlockedByIds(
            int tileId, 
            HashSet<int> removed, 
            Dictionary<int, SolverTileData> tileById)
        {
            if (!tileById.TryGetValue(tileId, out SolverTileData tile) || tile == null) return false;
            foreach (var coveringId in tile.coveringTileIds)
            {
                if (coveringId > 0 && !removed.Contains(coveringId)) return false;
            }
            return true;
        }

        private static List<int> BuildPlanIdsFromSegments(
            List<List<SolverStep>> committedSegments, 
            List<SolverStep> currentSegment)
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

        private static bool IsReplayUnlockable(
            List<int> planTileIds, 
            Dictionary<int, SolverTileData> tileById)
        {
            HashSet<int> replayRemoved = new HashSet<int>();
            foreach (int id in planTileIds)
            {
                if (!IsUnlockedByIds(id, replayRemoved, tileById)) return false;
                replayRemoved.Add(id);
            }
            return true;
        }

        private static void ShuffleInPlace(List<int> values, System.Random rnd)
        {
            for (int i = 0; i < values.Count; i++)
            {
                int j = rnd.Next(i, values.Count);
                int tmp = values[i];
                values[i] = values[j];
                values[j] = tmp;
            }
        }

        /// <summary>
        /// Checks if there are any valid moves remaining on the board/conveyor, considering active formula state.
        /// </summary>
        public static bool HasValidMoves(Tile[] occupiedTiles, List<Tile> allExistingTiles, Tile[] occupiedConveyorTiles)
        {
            // 1. Collect available number and operator tiles
            List<Tile> availableNumbers = new List<Tile>();
            List<Tile> availableOperators = new List<Tile>();

            HashSet<Tile> conveyorSet = new HashSet<Tile>();
            if (occupiedConveyorTiles != null)
            {
                foreach (var t in occupiedConveyorTiles)
                {
                    if (t != null) conveyorSet.Add(t);
                }
            }

            HashSet<Tile> formulaSet = new HashSet<Tile>();
            if (occupiedTiles != null)
            {
                foreach (var t in occupiedTiles)
                {
                    if (t != null) formulaSet.Add(t);
                }
            }

            foreach (Tile tile in allExistingTiles)
            {
                if (tile == null || !tile.gameObject.activeInHierarchy || tile.type == TileType.Helper)
                    continue;

                if (formulaSet.Contains(tile))
                    continue;

                bool isAvailable = false;
                if (conveyorSet.Contains(tile))
                {
                    isAvailable = true;
                }
                else
                {
                    // Available if unlocked (no active covering tiles)
                    int activeCoveringCount = 0;
                    if (tile.coveringTiles != null)
                    {
                        foreach (var covering in tile.coveringTiles)
                        {
                            if (covering != null && covering.gameObject.activeInHierarchy && !formulaSet.Contains(covering))
                            {
                                activeCoveringCount++;
                            }
                        }
                    }

                    if (activeCoveringCount == 0)
                    {
                        isAvailable = true;
                    }
                }

                if (isAvailable)
                {
                    if (tile.type == TileType.Operator)
                    {
                        availableOperators.Add(tile);
                    }
                    else
                    {
                        availableNumbers.Add(tile);
                    }
                }
            }

            // 2. Check formula bar status
            bool formulaIsEmpty = true;
            if (occupiedTiles != null)
            {
                foreach (var t in occupiedTiles)
                {
                    if (t != null)
                    {
                        formulaIsEmpty = false;
                        break;
                    }
                }
            }

            if (formulaIsEmpty)
            {
                return CanFormValidFormula(availableNumbers, availableOperators);
            }
            else
            {
                // Try to complete current formula correctly
                Tile[] slotsCopy = (Tile[])occupiedTiles.Clone();
                if (CanCompleteCurrentFormula(slotsCopy, new List<Tile>(availableNumbers), new List<Tile>(availableOperators)))
                {
                    return true;
                }

                // If current formula cannot be completed correctly, simulate clearing the formula bar
                // and check if remaining available tiles can form a valid formula from scratch
                return CanFormValidFormula(availableNumbers, availableOperators);
            }
        }

        private static bool CanFormValidFormula(List<Tile> numbers, List<Tile> operators)
        {
            if (numbers.Count < 4 || operators.Count < 1)
                return false;

            Dictionary<int, int> numFreq = new Dictionary<int, int>();
            foreach (var num in numbers)
            {
                if (numFreq.ContainsKey(num.numberValue))
                    numFreq[num.numberValue]++;
                else
                    numFreq[num.numberValue] = 1;
            }

            for (int i = 0; i < numbers.Count; i++)
            {
                Tile n1 = numbers[i];
                int v1 = n1.numberValue;
                numFreq[v1]--;

                for (int j = 0; j < numbers.Count; j++)
                {
                    if (i == j) continue;
                    Tile n2 = numbers[j];
                    int v2 = n2.numberValue;
                    numFreq[v2]--;

                    foreach (Tile op in operators)
                    {
                        string opVal = op.operatorValue;
                        int leftSideResult = 0;
                        bool validOp = false;

                        switch (opVal)
                        {
                            case "+":
                                leftSideResult = v1 + v2;
                                validOp = true;
                                break;
                            case "-":
                                leftSideResult = v1 - v2;
                                validOp = true;
                                break;
                            case "x":
                            case "*":
                                leftSideResult = v1 * v2;
                                validOp = true;
                                break;
                            case "/":
                                if (v2 != 0)
                                {
                                    leftSideResult = (int)Math.Floor(v1 / (double)v2);
                                    validOp = true;
                                }
                                break;
                        }

                        if (validOp && leftSideResult >= -99 && leftSideResult <= 99)
                        {
                            if (leftSideResult >= -9 && leftSideResult <= 9)
                            {
                                if (numFreq.TryGetValue(leftSideResult, out int count) && count > 0)
                                {
                                    numFreq[v2]++;
                                    numFreq[v1]++;
                                    return true;
                                }
                            }
                            else
                            {
                                int s4Val, s5Val;
                                if (leftSideResult >= 0)
                                {
                                    s4Val = leftSideResult / 10;
                                    s5Val = leftSideResult % 10;
                                }
                                else
                                {
                                    s4Val = -(Math.Abs(leftSideResult) / 10);
                                    s5Val = Math.Abs(leftSideResult) % 10;
                                }

                                if (s4Val == s5Val)
                                {
                                    if (numFreq.TryGetValue(s4Val, out int count) && count >= 2)
                                    {
                                        numFreq[v2]++;
                                        numFreq[v1]++;
                                        return true;
                                    }
                                }
                                else
                                {
                                    if (numFreq.TryGetValue(s4Val, out int c4) && c4 > 0 &&
                                        numFreq.TryGetValue(s5Val, out int c5) && c5 > 0)
                                    {
                                        numFreq[v2]++;
                                        numFreq[v1]++;
                                        return true;
                                    }
                                }
                            }
                        }
                    }

                    numFreq[v2]++;
                }

                numFreq[v1]++;
            }

            return false;
        }

        private static bool CanCompleteCurrentFormula(Tile[] slots, List<Tile> availableNumbers, List<Tile> availableOperators)
        {
            return TryFillSlot(0, slots, availableNumbers, availableOperators);
        }

        private static bool TryFillSlot(int slotIndex, Tile[] slots, List<Tile> availableNumbers, List<Tile> availableOperators)
        {
            if (slotIndex == 5)
            {
                if (FormulaEvaluator.Instance == null) return false;
                return FormulaEvaluator.Instance.CheckResult(slots);
            }

            if (slots[slotIndex] != null)
            {
                return TryFillSlot(slotIndex + 1, slots, availableNumbers, availableOperators);
            }

            if (slotIndex == 4)
            {
                if (FormulaEvaluator.Instance != null && FormulaEvaluator.Instance.CheckResult(slots))
                {
                    return true;
                }
            }

            if (slotIndex == 1) // Operator slot
            {
                for (int i = 0; i < availableOperators.Count; i++)
                {
                    Tile op = availableOperators[i];
                    slots[1] = op;
                    availableOperators.RemoveAt(i);

                    if (TryFillSlot(slotIndex + 1, slots, availableNumbers, availableOperators))
                    {
                        availableOperators.Insert(i, op);
                        slots[1] = null;
                        return true;
                    }

                    availableOperators.Insert(i, op);
                    slots[1] = null;
                }
            }
            else // Number slots: 0, 2, 3, 4
            {
                for (int i = 0; i < availableNumbers.Count; i++)
                {
                    Tile num = availableNumbers[i];
                    slots[slotIndex] = num;
                    availableNumbers.RemoveAt(i);

                    if (TryFillSlot(slotIndex + 1, slots, availableNumbers, availableOperators))
                    {
                        availableNumbers.Insert(i, num);
                        slots[slotIndex] = null;
                        return true;
                    }

                    availableNumbers.Insert(i, num);
                    slots[slotIndex] = null;
                }
            }

            return false;
        }
    }
}
