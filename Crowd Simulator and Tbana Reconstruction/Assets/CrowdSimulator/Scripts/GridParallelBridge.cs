using UnityEngine;
using Unity.Jobs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

public class GridParallelBridge : MonoBehaviour
{
    private static GridParallelBridge _instance;
    public static GridParallelBridge Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GridParallelBridge>();
            }
            return _instance;
        }
    }

    // Persistent native array to mimic the flattened simulation grid density
    public NativeArray<float> nativeDensityGrid;
    private int totalGridCells;
    private NativeMultiHashMap<int, int> nativeSpatialGrid;

    void Awake()
    {
        _instance = this;
    }

    public void InitializeGridData(int cellsX, int cellsZ)
    {
        totalGridCells = cellsX * cellsZ;
        // Allocating unmanaged memory that persists throughout the game's lifetime
        nativeDensityGrid = new NativeArray<float>(totalGridCells, Allocator.Persistent);
    }

    public void SetupSpatialGrid(int agentCapacity, int totalBins)
{
    if (nativeSpatialGrid.IsCreated) nativeSpatialGrid.Dispose();
    
    // Change 'maxAgents' to 'agentCapacity' (the parameter we pass in)
    nativeSpatialGrid = new NativeMultiHashMap<int, int>(agentCapacity, Allocator.Persistent);
}

    // Helper method to sync your old float[,] data into our flat NativeArray right before calculation
    public void CopyManagedGridToNative(float[,] managedDensity, int nCellsX, int nCellsZ)
    {
        for (int r = 0; r < nCellsZ; r++)
        {
            for (int c = 0; c < nCellsX; c++)
            {
                nativeDensityGrid[r * nCellsX + c] = managedDensity[r, c];
            }
        }
    }

    // This executes the multithreaded calculation loop across your list of agents
    public void CalculateAgentDensitiesInParallel(List<Agent> agentList, SimulationGrid grid)
    {
        int agentCount = agentList.Count;
        if (agentCount == 0) return;

        // 1. Allocate TempJob memory (blazing fast allocations that expire at the end of the frame)
        NativeArray<int> columns = new NativeArray<int>(agentCount, Allocator.TempJob);
        NativeArray<int> rows = new NativeArray<int>(agentCount, Allocator.TempJob);
        NativeArray<float> neighbourXWeights = new NativeArray<float>(agentCount, Allocator.TempJob);
        NativeArray<float> neighbourZWeights = new NativeArray<float>(agentCount, Allocator.TempJob);
        NativeArray<float> neighbourXZWeights = new NativeArray<float>(agentCount, Allocator.TempJob);
        NativeArray<float> selfWeights = new NativeArray<float>(agentCount, Allocator.TempJob);
        NativeArray<float> results = new NativeArray<float>(agentCount, Allocator.TempJob);

        // 2. Populate native structures with the managed agent field parameters
        for (int i = 0; i < agentCount; i++)
        {
            columns[i] = agentList[i].column;
            rows[i] = agentList[i].row;
            neighbourXWeights[i] = agentList[i].neighbourXWeight;
            neighbourZWeights[i] = agentList[i].neighbourZWeight;
            neighbourXZWeights[i] = agentList[i].neighbourXZWeight;
            selfWeights[i] = agentList[i].selfWeight;
        }

        // 3. Instantiate the job recipe and hand over data pointers
        CalculateDensityJob densityJob = new CalculateDensityJob
        {
            columns = columns,
            rows = rows,
            neighbourXWeights = neighbourXWeights,
            neighbourZWeights = neighbourZWeights,
            neighbourXZWeights = neighbourXZWeights,
            selfWeights = selfWeights,
            globalDensityGrid = this.nativeDensityGrid,
            nCellsX = grid.nCellsX,
            nCellsZ = grid.nCellsZ,
            outDensityAtAgentPosition = results
        };

        // 4. Schedule parallel processing! innerloopBatchCount of 16-64 is optimal for workload distribution.
        JobHandle handle = densityJob.Schedule(agentCount, 32);

        // 5. Wait for background threads to finish execution
        handle.Complete();

        // 6. Push the calculated values back into the live managed agents
        for (int i = 0; i < agentCount; i++)
        {
            agentList[i].densityAtAgentPosition = results[i];
        }

        // 7. Clean up our allocations to protect against memory corruption or leaks
        columns.Dispose();
        rows.Dispose();
        neighbourXWeights.Dispose();
        neighbourZWeights.Dispose();
        neighbourXZWeights.Dispose();
        selfWeights.Dispose();
        results.Dispose();
    }

    public void RunParallelCollisionAvoidance(List<Agent> agentList, SimulationGrid grid, Vector2 xMinMax, Vector2 zMinMax)
{
    int agentCount = agentList.Count;
    if (agentCount == 0) return;

    // 1. Allocate Temporary frame memory arrays
    NativeArray<float3> positions = new NativeArray<float3>(agentCount, Allocator.TempJob);
    NativeArray<float3> preferredVels = new NativeArray<float3>(agentCount, Allocator.TempJob);
    NativeArray<bool> isWaitingFlags = new NativeArray<bool>(agentCount, Allocator.TempJob);
    NativeArray<bool> isPreparingFlags = new NativeArray<bool>(agentCount, Allocator.TempJob);
    NativeArray<bool> doneFlags = new NativeArray<bool>(agentCount, Allocator.TempJob);
    NativeArray<float> walkingSpeeds = new NativeArray<float>(agentCount, Allocator.TempJob);
    NativeArray<float3> collisionForces = new NativeArray<float3>(agentCount, Allocator.TempJob);

    // Clear old data out of our multi-hash map bucket space
    nativeSpatialGrid.Clear();

    // 2. Extract current managed properties and populate the lookup grid hash bucket
    for (int i = 0; i < agentCount; i++)
{
    positions[i] = agentList[i].tr.position;
    preferredVels[i] = agentList[i].preferredVelocity;
    isWaitingFlags[i] = agentList[i].isWaiting;
    isPreparingFlags[i] = agentList[i].isPreparingToBoard;
    doneFlags[i] = agentList[i].done;
    walkingSpeeds[i] = agentList[i].walkingSpeed;

    // Calculate row/column cell hashes exactly like the original simulation loops
    int r = (int)((agentList[i].tr.position.z - zMinMax.x) / grid.lenOfBin);
    int c = (int)((agentList[i].tr.position.x - xMinMax.x) / grid.lenOfBin);
    r = Mathf.Clamp(r, 0, grid.neighbourBins - 1);
    c = Mathf.Clamp(c, 0, grid.neighbourBins - 1);

    int binKey = r * grid.neighbourBins + c;
    nativeSpatialGrid.Add(binKey, i); 
}

    // 3. Setup the parallel job execution settings
    CollisionAvoidanceJob collisionJob = new CollisionAvoidanceJob
    {
        agentPositions = positions,
        preferredVelocities = preferredVels,
        isWaitingFlags = isWaitingFlags,
        isPreparingFlags = isPreparingFlags,
        doneFlags = doneFlags,
        walkingSpeeds = walkingSpeeds,
        spatialGrid = nativeSpatialGrid,
        ringDiameter = grid.ringDiameter,
        lenOfBin = grid.lenOfBin,
        neighbourBins = grid.neighbourBins,
        xMinMax = new float3(xMinMax.x, xMinMax.y, 0f),
        zMinMax = new float3(zMinMax.x, zMinMax.y, 0f),
        outCollisionAvoidanceVelocity = collisionForces
    };

    // 4. Fire the job into multi-threaded processing
    JobHandle handle = collisionJob.Schedule(agentCount, 16);
    handle.Complete();

    // 5. Transfer computed parallel results directly back to the active tracking Monobehaviours
    for (int i = 0; i < agentCount; i++)
    {
        agentList[i].collisionAvoidanceVelocity = (Vector3)collisionForces[i];
    }

    // 6. Dispose of temporary arrays
    positions.Dispose();
    preferredVels.Dispose();
    isWaitingFlags.Dispose();
    isPreparingFlags.Dispose();
    doneFlags.Dispose();
    walkingSpeeds.Dispose();
    collisionForces.Dispose();
}

    void OnDestroy()
    {
        // Clean up persistent memory when exiting the scene
        if (nativeDensityGrid.IsCreated) nativeDensityGrid.Dispose();
        if (nativeSpatialGrid.IsCreated) nativeSpatialGrid.Dispose();
        if (_instance == this)
        {
            _instance = null;
        }
    }
}