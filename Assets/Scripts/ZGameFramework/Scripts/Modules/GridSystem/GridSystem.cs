using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ZGameFramework.Modules
{
    public class GridSystem<TGridObject>:AbstractSystem
    {
        private List<GridModel<TGridObject>> gridModelList;

        private int width;
        private int height;
        private float cellSize;
        private int floorAmount;
        private float floorHeight;
        private GridPlane gridPlane;

        public GridSystem(int width, int height, float cellSize, int floorAmount, float floorHeight, GridPlane plane, 
            Func<GridModel<TGridObject>, GridPosition, TGridObject> createGridObject)
        {
            this.width = width;
            this.height = height;
            this.cellSize = cellSize;
            this.floorAmount = floorAmount;
            this.floorHeight = floorHeight;
            this.gridPlane = plane;

            gridModelList = new List<GridModel<TGridObject>>();

            for (int floor = 0; floor < floorAmount; floor++)
            {
                GridModel<TGridObject> gridModel = new GridModel<TGridObject>(
                    width, height, cellSize, floor, floorHeight, gridPlane, createGridObject
                );
                gridModelList.Add(gridModel);
            }
        }

        public GridModel<TGridObject> GetGridSystem(int floor)
        {
            floor = Mathf.Clamp(floor, 0, floorAmount - 1);
            return gridModelList[floor];
        }

        public int GetFloor(Vector3 worldPosition)
        {
            int floor;
            if (gridPlane == GridPlane.XZ)
            {
                floor = Mathf.RoundToInt(worldPosition.y / floorHeight);
            }
            else
            {
                floor = Mathf.RoundToInt(worldPosition.z / floorHeight);
            }
            return Mathf.Clamp(floor, 0, floorAmount - 1);
        }

        public GridPosition GetGridPosition(Vector3 worldPosition)
        {
            int floor = GetFloor(worldPosition);
            return GetGridSystem(floor).GetGridPosition(worldPosition);
        }

        public Vector3 GetWorldPosition(GridPosition gridPosition)
        {
            return GetGridSystem(gridPosition.floor).GetWorldPosition(gridPosition);
        }

        public TGridObject GetGridObject(GridPosition gridPosition)
        {
            return GetGridSystem(gridPosition.floor).GetGridObject(gridPosition);
        }

        public bool IsValidPosition(GridPosition gridPosition)
        {
            if (gridPosition.floor < 0 || gridPosition.floor >= floorAmount) return false;
            return GetGridSystem(gridPosition.floor).IsValidPosition(gridPosition);
        }

        public int GetWidth() => width;
        public int GetHeight() => height;
        public int GetFloorAmount() => floorAmount;
        public float GetCellSize() => cellSize;
        protected override void OnInit()
        {
            
        }
    }
}
