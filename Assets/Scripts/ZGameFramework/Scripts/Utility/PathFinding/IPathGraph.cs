using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZGameFramework.Utility
{
    public interface IPathGraph<T>
    {
        public List<T> GetNeighbors(T node);

        public int GetMoveCost(T fromNode,T toNode);

        public int GetHeuristicCost(T fromNode, T toNode);
    }
}