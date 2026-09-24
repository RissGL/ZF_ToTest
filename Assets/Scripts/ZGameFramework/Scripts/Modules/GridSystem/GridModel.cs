using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Modules;

namespace ZGameFramework.Modules
{
    public class GridModel<TGridObject>:AbstractModel
    {
        private int width;
        private int height;
        private float cellSize;
        private TGridObject[,] gridObjects;
        private GridPlane gridPlane;

        private float floorHeight;

        int floor;


        public GridModel(int width, int height, float cellSize, int floor, float floorHeight,GridPlane gridPlane,
            Func<GridModel<TGridObject>, GridPosition, TGridObject> createGridObject)
        {
            this.width = width;
            this.height = height;
            this.cellSize = cellSize;
            this.floor = floor;
            this.floorHeight = floorHeight;
            gridObjects = new TGridObject[width, height];
            this.gridPlane = gridPlane;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    GridPosition gridPosition = new GridPosition(x, z, floor);
                    gridObjects[x, z] = createGridObject(this, gridPosition);
                }
            }
        }

        public Vector3 GetWorldPosition(GridPosition gridPosition)
        {
            if (gridPlane == GridPlane.XZ)
            {
                return new Vector3(gridPosition.x, 0, gridPosition.z) * cellSize
                    + new Vector3(0, gridPosition.floor, 0) * floorHeight;
            }
            else 
            {
                return new Vector3(gridPosition.x, gridPosition.z, 0) * cellSize
                    + new Vector3(0, 0, gridPosition.floor) * floorHeight;
            }

        }

        public GridPosition GetGridPosition(Vector3 worldPosition)
        {
            if (gridPlane == GridPlane.XZ)
            {
                return new GridPosition(
                Mathf.RoundToInt(worldPosition.x / cellSize),
                Mathf.RoundToInt(worldPosition.z / cellSize),
                floor
                );
            }
            else
            {
                return new GridPosition(
                Mathf.RoundToInt(worldPosition.x / cellSize),
                Mathf.RoundToInt(worldPosition.y / cellSize),
                floor
                );
            }
        }

        public TGridObject GetGridObject(GridPosition gridPosition)
        {
            return gridObjects[gridPosition.x, gridPosition.z];
        }

        public bool IsValidPosition(GridPosition gridPosition)
        {
            return gridPosition.x < width
                && gridPosition.z < height
                && gridPosition.x >= 0
                && gridPosition.z >= 0;
        }

        public int GetWidth()
        {
            return width;
        }

        public int GetHeight()
        {
            return height;
        }

        public int GetFloor()
        {
            return floor;
        }

        protected override void OnInit()
        {
            
        }
    }
}