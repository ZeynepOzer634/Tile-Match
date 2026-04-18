using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TileMatch.Editor
{
    public static class LevelGenerator
    {
        public class GeneratorResult
        {
            public Dictionary<Vector3Int, int> GridData;
            public List<int[]> Orders;
            public bool Success;
        }

        public static GeneratorResult Generate(int numOrders, int numTileTypes)
        {
            int totalTiles = numOrders * 3;
            
            for (int i = 0; i < 50; i++)
            {
                var layout = GenerateLayout(totalTiles);
                var result = SolveReverse(layout, numOrders, numTileTypes);
                if (result.Success)
                    return result;
            }

            return new GeneratorResult { Success = false };
        }

        private static List<Vector3Int> GenerateLayout(int totalTiles)
        {
            List<Vector3Int> layout = new List<Vector3Int>();
            List<Vector3Int> allSlots = new List<Vector3Int>();
            System.Random rng = new System.Random();

            int parity = 1;
            
            // 0: Classic Pyramid, 1: Cross
            int shapeType = rng.Next(2); 

            int maxLayers = 5;
            for (int z = 0; z < maxLayers; z++)
            {
                for (int x = -5; x <= 5; x++)
                {
                    for (int y = -5; y <= 5; y++)
                    {
                        if (Math.Abs(x % 2) == Math.Abs((z + parity) % 2) && 
                            Math.Abs(y % 2) == Math.Abs((z + parity) % 2))
                        {
                            bool validShape = true;
                            // Cross shape: remove the 4 corners
                            if (shapeType == 1 && Math.Abs(x) >= 3 && Math.Abs(y) >= 3) validShape = false;

                            if (validShape)
                            {
                                if (z > 0)
                                {
                                    Vector3Int[] neededSupports = new Vector3Int[]
                                    {
                                        new Vector3Int(x - 1, y - 1, z - 1),
                                        new Vector3Int(x + 1, y - 1, z - 1),
                                        new Vector3Int(x - 1, y + 1, z - 1),
                                        new Vector3Int(x + 1, y + 1, z - 1)
                                    };

                                    bool hasAllSupport = true;
                                    foreach (var req in neededSupports)
                                    {
                                        if (!allSlots.Contains(req))
                                        {
                                            hasAllSupport = false;
                                            break;
                                        }
                                    }

                                    if (hasAllSupport) allSlots.Add(new Vector3Int(x, y, z));
                                }
                                else
                                {
                                    allSlots.Add(new Vector3Int(x, y, z));
                                }
                            }
                        }
                    }
                }
            }

            if (allSlots.Count <= totalTiles) return allSlots;

            while (allSlots.Count > totalTiles)
            {
                List<Vector3Int> exposed = new List<Vector3Int>();
                foreach (var slot in allSlots)
                {
                    bool isExposed = true;
                    Vector3Int[] above = new Vector3Int[]
                    {
                        new Vector3Int(slot.x - 1, slot.y - 1, slot.z + 1),
                        new Vector3Int(slot.x + 1, slot.y - 1, slot.z + 1),
                        new Vector3Int(slot.x - 1, slot.y + 1, slot.z + 1),
                        new Vector3Int(slot.x + 1, slot.y + 1, slot.z + 1)
                    };

                    foreach (var a in above)
                    {
                        if (allSlots.Contains(a))
                        {
                            isExposed = false;
                            break;
                        }
                    }

                    if (isExposed) exposed.Add(slot);
                }

                // We want to strongly prefer removing EXPOSED tiles at the BOTTOM (Z=0).
                // This ensures the player can't immediately click Z=0 tiles without digging from the top.
                exposed.Sort((a, b) => 
                {
                    if (a.z != b.z) return a.z.CompareTo(b.z); // Ascending Z (lower Z comes first)
                    
                    float distA = a.x * a.x + a.y * a.y;
                    float distB = b.x * b.x + b.y * b.y;
                    return distB.CompareTo(distA); // Furthest comes first
                });

                // Pick from the absolute best candidates to keep the pyramid steep
                int pickPoolSize = Math.Max(1, Math.Min(3, exposed.Count / 3));
                int pickIndex = rng.Next(pickPoolSize);
                
                allSlots.Remove(exposed[pickIndex]);
            }

            layout.AddRange(allSlots);
            return layout;
        }

        private static GeneratorResult SolveReverse(List<Vector3Int> layout, int numOrders, int numTileTypes)
        {
            int maxAttempts = 1000;
            System.Random rng = new System.Random();

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var result = TrySolveReverse(layout, numOrders, numTileTypes, rng);
                if (result.Success) return result;
            }

            return new GeneratorResult { Success = false };
        }

        private static GeneratorResult TrySolveReverse(List<Vector3Int> layout, int numOrders, int numTileTypes, System.Random rng)
        {
            HashSet<Vector3Int> filled = new HashSet<Vector3Int>();
            List<int> rack = new List<int>();
            Dictionary<Vector3Int, int> gridData = new Dictionary<Vector3Int, int>();
            List<int[]> generatedOrders = new List<int[]>();

            Dictionary<Vector3Int, List<Vector3Int>> dependencies = new Dictionary<Vector3Int, List<Vector3Int>>();
            foreach (var s in layout)
            {
                dependencies[s] = new List<Vector3Int>();
                foreach (var s1 in layout)
                {
                    if (s1.z < s.z && Overlaps(s, s1))
                    {
                        dependencies[s].Add(s1);
                    }
                }
            }

            int currentOrderId = 0;

            while (filled.Count < layout.Count || rack.Count > 0)
            {
                List<Vector3Int> availableSlots = new List<Vector3Int>();
                foreach (var s in layout)
                {
                    if (!filled.Contains(s))
                    {
                        bool allDepsMet = true;
                        foreach (var dep in dependencies[s])
                        {
                            if (!filled.Contains(dep))
                            {
                                allDepsMet = false;
                                break;
                            }
                        }
                        if (allDepsMet) availableSlots.Add(s);
                    }
                }

                List<Action> validMoves = new List<Action>();

                if (rack.Count > 0 && availableSlots.Count > 0)
                {
                    int rackIdx = rng.Next(rack.Count);
                    int item = rack[rackIdx];
                    
                    for (int i = 0; i < Math.Min(3, availableSlots.Count); i++)
                    {
                        Vector3Int slot = availableSlots[rng.Next(availableSlots.Count)];
                        validMoves.Add(() =>
                        {
                            rack.RemoveAt(rackIdx);
                            filled.Add(slot);
                            gridData[slot] = item;
                        });
                    }
                }

                if (currentOrderId < numOrders)
                {
                    int rSpace = 6 - rack.Count;
                    int maxK = Math.Min(3, availableSlots.Count);
                    int minK = Math.Max(0, 3 - rSpace);

                    if (minK <= maxK)
                    {
                        for (int k = minK; k <= maxK; k++)
                        {
                            List<Vector3Int> slotsCopy = new List<Vector3Int>(availableSlots);
                            List<Vector3Int> chosenSlots = new List<Vector3Int>();
                            for (int i = 0; i < k; i++)
                            {
                                int idx = rng.Next(slotsCopy.Count);
                                chosenSlots.Add(slotsCopy[idx]);
                                slotsCopy.RemoveAt(idx);
                            }

                            int orderId = currentOrderId;
                            int capturedK = k;
                            validMoves.Add(() =>
                            {
                                foreach (var s in chosenSlots)
                                {
                                    filled.Add(s);
                                    gridData[s] = orderId;
                                }
                                for (int i = 0; i < 3 - capturedK; i++)
                                {
                                    rack.Add(orderId);
                                }
                                generatedOrders.Add(new int[] { orderId, orderId, orderId });
                                currentOrderId++;
                            });
                        }
                    }
                }

                if (validMoves.Count == 0) return new GeneratorResult { Success = false };

                validMoves[rng.Next(validMoves.Count)].Invoke();
            }

            Dictionary<int, List<Vector3Int>> orderSlots = new Dictionary<int, List<Vector3Int>>();
            for (int i = 0; i < numOrders; i++) orderSlots[i] = new List<Vector3Int>();

            foreach (var kvp in gridData)
            {
                orderSlots[kvp.Value].Add(kvp.Key);
            }

            Dictionary<Vector3Int, int> finalGridData = new Dictionary<Vector3Int, int>();
            Dictionary<int, int[]> finalOrderTypes = new Dictionary<int, int[]>();

            foreach (var kvp in orderSlots)
            {
                int orderId = kvp.Key;
                List<Vector3Int> slots = kvp.Value;
                
                slots.Sort((a, b) => b.z.CompareTo(a.z));

                int[] types = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    int randomType = rng.Next(numTileTypes);
                    types[j] = randomType;
                    finalGridData[slots[j]] = randomType;
                }
                finalOrderTypes[orderId] = types;
            }

            List<int[]> finalOrders = new List<int[]>();
            generatedOrders.Reverse();
            foreach (var orderInfo in generatedOrders)
            {
                int orderId = orderInfo[0];
                finalOrders.Add(finalOrderTypes[orderId]);
            }

            return new GeneratorResult
            {
                Success = true,
                GridData = finalGridData,
                Orders = finalOrders
            };
        }

        private static bool Overlaps(Vector3Int a, Vector3Int b)
        {
            return (a.x < b.x + 2 && a.x + 2 > b.x &&
                    a.y < b.y + 2 && a.y + 2 > b.y);
        }

        // ==========================================
        // VALIDATION
        // ==========================================
        public static bool ValidateLevel(Dictionary<Vector3Int, int> gridData, List<int[]> orders)
        {
            // Initial counts check
            int totalReq = orders.Count * 3;
            if (gridData.Count != totalReq) return false;

            Dictionary<int, int> boardCounts = new Dictionary<int, int>();
            foreach (var t in gridData.Values)
            {
                if (!boardCounts.ContainsKey(t)) boardCounts[t] = 0;
                boardCounts[t]++;
            }
            Dictionary<int, int> orderCounts = new Dictionary<int, int>();
            foreach (var o in orders)
            {
                foreach (var t in o)
                {
                    if (!orderCounts.ContainsKey(t)) orderCounts[t] = 0;
                    orderCounts[t]++;
                }
            }
            foreach (var kvp in orderCounts)
            {
                if (!boardCounts.ContainsKey(kvp.Key) || boardCounts[kvp.Key] != kvp.Value) return false;
            }

            // Prepare dependency graph for quick blocked checking
            Dictionary<Vector3Int, List<Vector3Int>> blocks = new Dictionary<Vector3Int, List<Vector3Int>>();
            List<Vector3Int> allTiles = new List<Vector3Int>(gridData.Keys);
            foreach (var s in allTiles)
            {
                blocks[s] = new List<Vector3Int>();
                foreach (var s2 in allTiles)
                {
                    if (s2.z > s.z && Overlaps(s, s2))
                    {
                        blocks[s].Add(s2);
                    }
                }
            }

            HashSet<string> visited = new HashSet<string>();
            return DFSValidate(new HashSet<Vector3Int>(allTiles), new List<int>(), 0, 0, gridData, orders, blocks, visited);
        }

        private static bool DFSValidate(
            HashSet<Vector3Int> board, 
            List<int> rack, 
            int oIdx, 
            int iIdx, 
            Dictionary<Vector3Int, int> gridData, 
            List<int[]> orders, 
            Dictionary<Vector3Int, List<Vector3Int>> blocks,
            HashSet<string> visited)
        {
            if (oIdx >= orders.Count) return true; // Won!

            // Memoization to prevent loops / extreme branch explosion
            string rackStr = string.Join(",", rack.OrderBy(x => x));
            string stateKey = $"{board.Count}|{oIdx}|{iIdx}|{rackStr}";
            if (visited.Contains(stateKey)) return false;
            visited.Add(stateKey);

            int neededType = orders[oIdx][iIdx];

            // 1. Is it in the rack? (Highest priority, uses no board moves, frees rack space)
            if (rack.Contains(neededType))
            {
                List<int> newRack = new List<int>(rack);
                newRack.Remove(neededType);
                int nextI = iIdx + 1;
                int nextO = oIdx;
                if (nextI >= 3) { nextI = 0; nextO++; }
                
                if (DFSValidate(board, newRack, nextO, nextI, gridData, orders, blocks, visited)) return true;
                return false; // If we couldn't win by taking it from rack, we won't win otherwise because it's always strictly better to take from rack.
            }

            // Find all unblocked tiles
            List<Vector3Int> unblocked = new List<Vector3Int>();
            foreach (var tile in board)
            {
                bool isBlocked = false;
                foreach (var blocker in blocks[tile])
                {
                    if (board.Contains(blocker))
                    {
                        isBlocked = true;
                        break;
                    }
                }
                if (!isBlocked) unblocked.Add(tile);
            }

            // 2. Can we click an unblocked tile of the needed type?
            bool foundDirectMatch = false;
            foreach (var tile in unblocked)
            {
                if (gridData[tile] == neededType)
                {
                    foundDirectMatch = true;
                    HashSet<Vector3Int> newBoard = new HashSet<Vector3Int>(board);
                    newBoard.Remove(tile);
                    int nextI = iIdx + 1;
                    int nextO = oIdx;
                    if (nextI >= 3) { nextI = 0; nextO++; }

                    if (DFSValidate(newBoard, rack, nextO, nextI, gridData, orders, blocks, visited)) return true;
                }
            }
            if (foundDirectMatch) return false; // Greedy choice: if direct match exists, we shouldn't purposefully fill rack

            // 3. We must click something to unblock our needed tile. (Adds to rack)
            if (rack.Count >= 6) return false; // Fail state

            foreach (var tile in unblocked)
            {
                HashSet<Vector3Int> newBoard = new HashSet<Vector3Int>(board);
                newBoard.Remove(tile);
                List<int> newRack = new List<int>(rack);
                newRack.Add(gridData[tile]);

                if (DFSValidate(newBoard, newRack, oIdx, iIdx, gridData, orders, blocks, visited)) return true;
            }

            return false;
        }
    }
}
