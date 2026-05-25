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

    // Persistent native arrays for LCP solver
    public NativeArray<float> nativeAvailableArea;
    public NativeArray<float> nativeDensity;
    public NativeArray<float> nativeXEdgeDensity;
    public NativeArray<float> nativeZEdgeDensity;
    public NativeArray<float> nativeXEdgeVelocity;
    public NativeArray<float> nativeZEdgeVelocity;
    
    public NativeArray<double> nativeXArray;
    public NativeArray<double> nativeBArray;
    public NativeArray<double> nativeLArray;
    public NativeArray<CellCoefficients> nativeCoeffs;
    
    public NativeArray<float3> nativeXEdgeVelocityVectors;
    public NativeArray<float3> nativeZEdgeVelocityVectors;
    
    private bool arraysInitialized = false;

    void Awake()
    {
        _instance = this;
    }

    public void InitializeGridData(int cellsX, int cellsZ)
    {
        totalGridCells = cellsX * cellsZ;
        // Allocating unmanaged memory that persists throughout the game's lifetime
        nativeDensityGrid = new NativeArray<float>(totalGridCells, Allocator.Persistent);

        int totalXEdges = cellsZ * (cellsX + 1);
        int totalZEdges = (cellsZ + 1) * cellsX;

        nativeAvailableArea = new NativeArray<float>(totalGridCells, Allocator.Persistent);
        nativeDensity = new NativeArray<float>(totalGridCells, Allocator.Persistent);
        
        nativeXEdgeDensity = new NativeArray<float>(totalXEdges, Allocator.Persistent);
        nativeZEdgeDensity = new NativeArray<float>(totalZEdges, Allocator.Persistent);
        nativeXEdgeVelocity = new NativeArray<float>(totalXEdges, Allocator.Persistent);
        nativeZEdgeVelocity = new NativeArray<float>(totalZEdges, Allocator.Persistent);

        nativeXArray = new NativeArray<double>(totalGridCells, Allocator.Persistent);
        nativeBArray = new NativeArray<double>(totalGridCells, Allocator.Persistent);
        nativeLArray = new NativeArray<double>(totalGridCells, Allocator.Persistent);
        nativeCoeffs = new NativeArray<CellCoefficients>(totalGridCells, Allocator.Persistent);

        nativeXEdgeVelocityVectors = new NativeArray<float3>(totalXEdges, Allocator.Persistent);
        nativeZEdgeVelocityVectors = new NativeArray<float3>(totalZEdges, Allocator.Persistent);

        arraysInitialized = true;
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

    public void CopyAvailableAreaToNative(SimulationGrid grid)
    {
        for (int r = 0; r < grid.nCellsZ; r++)
        {
            for (int c = 0; c < grid.nCellsX; c++)
            {
                nativeAvailableArea[r * grid.nCellsX + c] = grid.cellMatrix[r, c].availableArea;
            }
        }
    }

    public void CopyManagedGridsToNative(SimulationGrid grid)
    {
        int cellsX = grid.nCellsX;
        int cellsZ = grid.nCellsZ;

        // 1. Density
        for (int r = 0; r < cellsZ; r++)
        {
            for (int c = 0; c < cellsX; c++)
            {
                nativeDensity[r * cellsX + c] = grid.density[r, c];
            }
        }

        // 2. X Edge Density & Velocity
        for (int r = 0; r < cellsZ; r++)
        {
            for (int c = 0; c <= cellsX; c++)
            {
                int index = r * (cellsX + 1) + c;
                nativeXEdgeDensity[index] = grid.xEdgeDensity[r, c];
                nativeXEdgeVelocity[index] = grid.xEdgeVelocity[r, c];
            }
        }

        // 3. Z Edge Density & Velocity
        for (int r = 0; r <= cellsZ; r++)
        {
            for (int c = 0; c < cellsX; c++)
            {
                int index = r * cellsX + c;
                nativeZEdgeDensity[index] = grid.zEdgeDensity[r, c];
                nativeZEdgeVelocity[index] = grid.zEdgeVelocity[r, c];
            }
        }

        // 4. Initial values of solution xArray
        for (int i = 0; i < totalGridCells; i++)
        {
            nativeXArray[i] = grid.xArray[i];
            nativeLArray[i] = grid.lArray[i];
        }
    }

    public void CopyNativeSolutionToManaged(SimulationGrid grid)
    {
        for (int i = 0; i < totalGridCells; i++)
        {
            grid.xArray[i] = nativeXArray[i];
        }
    }

    public void CopyNativeVelocitiesToManaged(SimulationGrid grid)
    {
        int cellsX = grid.nCellsX;
        int cellsZ = grid.nCellsZ;

        for (int r = 0; r < cellsZ; r++)
        {
            for (int c = 0; c <= cellsX; c++)
            {
                int index = r * (cellsX + 1) + c;
                grid.xEdgeVelocity[r, c] = nativeXEdgeVelocity[index];
                grid.xEdgeVelocityNodeMatrix[r, c].velocity = nativeXEdgeVelocity[index];
            }
        }

        for (int r = 0; r <= cellsZ; r++)
        {
            for (int c = 0; c < cellsX; c++)
            {
                int index = r * cellsX + c;
                grid.zEdgeVelocity[r, c] = nativeZEdgeVelocity[index];
                grid.zEdgeVelocityNodeMatrix[r, c].velocity = nativeZEdgeVelocity[index];
            }
        }
    }

    public void CopyManagedEdgeVelocityVectorsToNative(SimulationGrid grid)
    {
        for (int r = 0; r < grid.nCellsZ; r++)
        {
            for (int c = 0; c <= grid.nCellsX; c++)
            {
                int index = r * (grid.nCellsX + 1) + c;
                nativeXEdgeVelocityVectors[index] = grid.xEdgeVelocityNodeMatrix[r, c].velocityVector;
            }
        }
        for (int r = 0; r <= grid.nCellsZ; r++)
        {
            for (int c = 0; c < grid.nCellsX; c++)
            {
                int index = r * grid.nCellsX + c;
                nativeZEdgeVelocityVectors[index] = grid.zEdgeVelocityNodeMatrix[r, c].velocityVector;
            }
        }
    }

    public void CopyNativeEdgeVelocityVectorsToManaged(SimulationGrid grid)
    {
        for (int r = 0; r < grid.nCellsZ; r++)
        {
            for (int c = 0; c <= grid.nCellsX; c++)
            {
                int index = r * (grid.nCellsX + 1) + c;
                grid.xEdgeVelocityNodeMatrix[r, c].velocityVector = (Vector3)nativeXEdgeVelocityVectors[index];
                grid.xEdgeVelocityNodeMatrix[r, c].velocity = nativeXEdgeVelocity[index];
            }
        }
        for (int r = 0; r <= grid.nCellsZ; r++)
        {
            for (int c = 0; c < grid.nCellsX; c++)
            {
                int index = r * grid.nCellsX + c;
                grid.zEdgeVelocityNodeMatrix[r, c].velocityVector = (Vector3)nativeZEdgeVelocityVectors[index];
                grid.zEdgeVelocityNodeMatrix[r, c].velocity = nativeZEdgeVelocity[index];
            }
        }
    }

    private static int agentLayerMask = -1;

    private struct RaycastMapping
    {
        public Agent agent;
        public int modifier;
    }

    public void BatchAndRunFrameRaycasts(List<Agent> agentList, MapGen.map roadmap, SimulationGrid grid)
    {
        int agentCount = agentList.Count;
        if (agentCount == 0) return;

        if (agentLayerMask == -1)
        {
            agentLayerMask = ~LayerMask.GetMask("WaitingAgent", "Agent");
        }

        int queryCount = 0;
        for (int i = 0; i < agentCount; i++)
        {
            Agent agent = agentList[i];
            agent.hasCachedCanSeeNext_0 = false;
            agent.hasCachedCanSeeNext_1 = false;

            if (agent.done || agent.isWaitingForDelay || agent.isWaiting || agent.isPreparingToBoard || agent.boarding)
                continue;

            if (agent.path != null && agent.pathIndex < agent.path.Count)
            {
                queryCount++;
                if (agent.pathIndex + 1 < agent.path.Count)
                {
                    queryCount++;
                }
            }
        }

        if (queryCount == 0) return;

        NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(queryCount, Allocator.TempJob);
        NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(queryCount, Allocator.TempJob);

        RaycastMapping[] mappings = new RaycastMapping[queryCount];

        int cmdIndex = 0;
        for (int i = 0; i < agentCount; i++)
        {
            Agent agent = agentList[i];
            if (agent.done || agent.isWaitingForDelay || agent.isWaiting || agent.isPreparingToBoard || agent.boarding)
                continue;

            if (agent.path != null && agent.pathIndex < agent.path.Count)
            {
                Vector3 pos = agent.tr.position;
                Vector3 targetPos = pos - agent.tr.forward * agent.colliderRadius;

                // Modifier 0
                Vector3 next0 = roadmap.allNodes[agent.path[agent.pathIndex]].getTargetPoint(pos, agent.gameObject.GetInstanceID());
                Vector3 dir = next0 - targetPos;
                float dist = dir.magnitude;
                
                commands[cmdIndex] = new RaycastCommand(targetPos, dir.normalized, dist, agentLayerMask);
                mappings[cmdIndex] = new RaycastMapping { agent = agent, modifier = 0 };
                cmdIndex++;

                // Modifier 1
                if (agent.pathIndex + 1 < agent.path.Count)
                {
                    Vector3 next1 = roadmap.allNodes[agent.path[agent.pathIndex + 1]].getTargetPoint(pos, agent.gameObject.GetInstanceID());
                    Vector3 dir1 = next1 - targetPos;
                    float dist1 = dir1.magnitude;

                    commands[cmdIndex] = new RaycastCommand(targetPos, dir1.normalized, dist1, agentLayerMask);
                    mappings[cmdIndex] = new RaycastMapping { agent = agent, modifier = 1 };
                    cmdIndex++;
                }
            }
        }

        JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 32);
        handle.Complete();

        for (int i = 0; i < queryCount; i++)
        {
            Agent agent = mappings[i].agent;
            bool clear = results[i].collider == null;

            if (mappings[i].modifier == 0)
            {
                agent.cachedCanSeeNext_0 = clear;
                agent.hasCachedCanSeeNext_0 = true;
            }
            else
            {
                agent.cachedCanSeeNext_1 = clear;
                agent.hasCachedCanSeeNext_1 = true;
            }
        }

        commands.Dispose();
        results.Dispose();
    }

    void OnDestroy()
    {
        // Clean up persistent memory when exiting the scene
        if (nativeDensityGrid.IsCreated) nativeDensityGrid.Dispose();
        if (nativeSpatialGrid.IsCreated) nativeSpatialGrid.Dispose();
        
        if (arraysInitialized)
        {
            if (nativeAvailableArea.IsCreated) nativeAvailableArea.Dispose();
            if (nativeDensity.IsCreated) nativeDensity.Dispose();
            if (nativeXEdgeDensity.IsCreated) nativeXEdgeDensity.Dispose();
            if (nativeZEdgeDensity.IsCreated) nativeZEdgeDensity.Dispose();
            if (nativeXEdgeVelocity.IsCreated) nativeXEdgeVelocity.Dispose();
            if (nativeZEdgeVelocity.IsCreated) nativeZEdgeVelocity.Dispose();
            if (nativeXArray.IsCreated) nativeXArray.Dispose();
            if (nativeBArray.IsCreated) nativeBArray.Dispose();
            if (nativeLArray.IsCreated) nativeLArray.Dispose();
            if (nativeCoeffs.IsCreated) nativeCoeffs.Dispose();
            if (nativeXEdgeVelocityVectors.IsCreated) nativeXEdgeVelocityVectors.Dispose();
            if (nativeZEdgeVelocityVectors.IsCreated) nativeZEdgeVelocityVectors.Dispose();
            arraysInitialized = false;
        }

        if (_instance == this)
        {
            _instance = null;
        }
    }
}