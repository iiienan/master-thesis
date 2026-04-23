using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
*   This script manages one waiting area.
*   It generates a grid of waiting spots and keeps track of which spots are occupied.
*/
public class WaitingArea : MonoBehaviour
{
    // Grid based on number of rows and columns
    public int rows = 3;
    public int columns = 5;
    private bool debug = false;
    private float waitingSpotSize = 0.5f;
    public int priority = 1;

    internal List<Vector3> waitingSpots;
    internal List<int> freeWaitingSpots;
    internal int mapIndex;
    private bool useRowColumns = false;
    public Material[] priorityMaterials;
    private Renderer areaRenderer;

    public void Initialize(bool debug, float waitingSpotSize, bool useRowColumns)
    {
        this.debug = debug;
        this.waitingSpotSize = waitingSpotSize;
        this.useRowColumns = useRowColumns;
        if(useRowColumns)
        {
            GenerateRowColumnWaitingSpots();
        }   
        else
        {
            GenerateFixedSizeWaitingSpots();
        }
        freeWaitingSpots = Enumerable.Range(0, waitingSpots.Count).ToList();
        
        if(!debug)
        {
            GetAreaRenderer().enabled = false;
        }
    }

    private Renderer GetAreaRenderer()
    {
        if (areaRenderer == null)
            areaRenderer = transform.Find("Area").GetComponent<Renderer>();
        return areaRenderer;
    }

    public float GetArea()
    {
        Vector3 size = GetAreaRenderer().bounds.size;
        return size.x * size.z;
    }

    // Returns the percentage of occupied spots in the waiting area.
    public float GetDensity()
    {
        float density = 1f - (freeWaitingSpots.Count / (float)waitingSpots.Count);
        return density;
    }

    /*
    *   Generates a grid of waiting spots for this waiting area 
    *   based on the number of rows and columns set by the user in the editor.
    */
    void GenerateRowColumnWaitingSpots()
    {
        Vector3 size = GetAreaRenderer().bounds.size;

        float cellWidth = size.x / columns;
        float cellHeight = size.z / rows;

        GenerateGrid(rows, columns, cellWidth, cellHeight);
    }

    void GenerateFixedSizeWaitingSpots()
    {
        Vector3 size = GetAreaRenderer().bounds.size;

        int columns = Mathf.FloorToInt(size.x / waitingSpotSize);
        int rows = Mathf.FloorToInt(size.z / waitingSpotSize);

        GenerateGrid(rows, columns, waitingSpotSize, waitingSpotSize);

    }

    private void GenerateGrid(int rows, int columns, float cellWidth, float cellHeight)
    {
        waitingSpots = new List<Vector3>();

        Renderer renderer = GetAreaRenderer();
        Vector3 corner = new Vector3(renderer.bounds.min.x, transform.position.y, renderer.bounds.min.z);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                float xPos = corner.x + cellWidth * 0.5f + col * cellWidth;
                float zPos = corner.z + cellHeight * 0.5f + row * cellHeight;
                Vector3 spotPosition = new Vector3(xPos, transform.position.y, zPos);
                waitingSpots.Add(spotPosition);

                if (debug)
                {
                    Debug.DrawLine(spotPosition, spotPosition + Vector3.up * 0.5f, Color.red, 10f);
                }
            }
        }
    }

    public int NWaitingSpots(float waitingSpotSize)
    {
        Vector3 size = GetAreaRenderer().bounds.size;

        int columns = Mathf.FloorToInt(size.x / waitingSpotSize);
        int rows = Mathf.FloorToInt(size.z / waitingSpotSize);

        return columns * rows;
    }

    /*
    *   The waiting area is also a node used for the agents' pathfinding.
    *   All nodes in the scene are stored in a list in the MapGen script.
    *   This is the index of this node in that list.
    */
    public void SetMapIndex(int index)
    {
        mapIndex = index;
    }

    /*
    *   Finds a free waiting spot in the waiting area.
    *   Returns the index of the waiting area (in the MapGen roadmap) and the index of the waiting spot (in the waitingSpots list).
    *   If there are no free spots, returns (-1, -1).
    */
    public (int index, int waitingSpot) GetWaitingSpot()
    {
        // If there are available spots
        if(HasFreeWaitingSpots())
        {
            int waitingSpot = GetRandomWaitingSpot();
            return (mapIndex, waitingSpot);
        }

        // No available spots
        return (-1, -1);
    }

    public bool HasFreeWaitingSpots()
    {
        return freeWaitingSpots.Count > 0;
    }

    private int GetRandomWaitingSpot()
    {
        int randomIndex = Random.Range(0, freeWaitingSpots.Count);
        int waitingSpot = freeWaitingSpots[randomIndex];
        freeWaitingSpots.RemoveAt(randomIndex);
        return waitingSpot;
    }

}
