using System.Collections.Generic;
using ZGameFramework.Core;

namespace ZGameFramework.Utility
{
    public static class AStarPathfinder
    {
        public static List<T> FindPath<T>(IPathGraph<T> graph, T start, T end, out int totalCost)
            where T : System.IEquatable<T>
        {
            totalCost = 0;

            List<T> openList = ListPool<T>.Get();

            Dictionary<T, int> gCostDict = DictionaryPool<T, int>.Get();
            Dictionary<T, int> fCostDict = DictionaryPool<T, int>.Get();

            Dictionary<T, T> cameFrom = DictionaryPool<T, T>.Get();

            openList.Add(start);
            gCostDict[start] = 0;
            fCostDict[start] = graph.GetHeuristicCost(start, end);

            List<T> finalPath = null;

            while (openList.Count > 0)
            {
                T current = GetLowestFCostNode(openList, fCostDict);

                if (current.Equals(end))
                {
                    totalCost = gCostDict[current];
                    finalPath = RetracePath(cameFrom, current);
                    break; 
                }

                openList.Remove(current);

                List<T> neighbors = graph.GetNeighbors(current);

                foreach (T neighbor in neighbors)
                {
                    int tentativeGCost = gCostDict[current] + graph.GetMoveCost(current, neighbor);

                    if (!gCostDict.ContainsKey(neighbor) || tentativeGCost < gCostDict[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gCostDict[neighbor] = tentativeGCost;
                        fCostDict[neighbor] = tentativeGCost + graph.GetHeuristicCost(neighbor, end);

                        if (!openList.Contains(neighbor))
                        {
                            openList.Add(neighbor);
                        }
                    }
                }

                ListPool<T>.Recycle(neighbors);
            }

            ListPool<T>.Recycle(openList);
            DictionaryPool<T, int>.Recycle(gCostDict);
            DictionaryPool<T, int>.Recycle(fCostDict);
            DictionaryPool<T, T>.Recycle(cameFrom);

            return finalPath;
        }

        private static List<T> RetracePath<T>(Dictionary<T, T> cameFrom, T current)
        {
            List<T> path = new List<T>();
            path.Add(current);

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        private static T GetLowestFCostNode<T>(List<T> openList, Dictionary<T, int> fCostDict)
        {
            T lowestNode = openList[0];
            int lowestFCost = fCostDict.ContainsKey(lowestNode) ? fCostDict[lowestNode] : int.MaxValue;

            for (int i = 1; i < openList.Count; i++)
            {
                T node = openList[i];
                int fCost = fCostDict.ContainsKey(node) ? fCostDict[node] : int.MaxValue;
                if (fCost < lowestFCost)
                {
                    lowestFCost = fCost;
                    lowestNode = node;
                }
            }
            return lowestNode;
        }
    }
}