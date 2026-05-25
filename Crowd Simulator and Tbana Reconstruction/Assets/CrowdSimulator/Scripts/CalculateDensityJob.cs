using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile] // This attribute tells Unity to compile this into ultra-fast machine code
public struct CalculateDensityJob : IJobParallelFor
{
    // 1. INPUT DATA: ReadOnly arrays are safe for multi-threading
    [ReadOnly] public NativeArray<int> columns;
    [ReadOnly] public NativeArray<int> rows;
    [ReadOnly] public NativeArray<float> neighbourXWeights;
    [ReadOnly] public NativeArray<float> neighbourZWeights;
    [ReadOnly] public NativeArray<float> neighbourXZWeights;
    [ReadOnly] public NativeArray<float> selfWeights;
    
    // The flattened 1D density grid array (replaces float[,])
    [ReadOnly] public NativeArray<float> globalDensityGrid;

    // Grid configuration variables
    public int nCellsX;
    public int nCellsZ;

    // 2. OUTPUT DATA: Each thread writes to its own agent index
    [WriteOnly] public NativeArray<float> outDensityAtAgentPosition;

    // This is called automatically by Unity's worker threads for each agent index
    public void Execute(int index)
    {
        // Extract this agent's local parameters from the arrays
        int column = columns[index];
        int row = rows[index];
        float selfWeight = selfWeights[index];
        float neighbourXWeight = neighbourXWeights[index];
        float neighbourZWeight = neighbourZWeights[index];
        float neighbourXZWeight = neighbourXZWeights[index];

        // Determine neighbor positions using logic that perfectly matches Unity's Mathf.Sign (x >= 0 ? 1 : -1)
        int xNeighbour = column + (neighbourXWeight >= 0f ? 1 : -1);
        int zNeighbour = row + (neighbourZWeight >= 0f ? 1 : -1);

        // Start calculating the density for this agent using optimized Unity.Mathematics functions
        float agentDensity = Unity.Mathematics.math.abs(selfWeight) * globalDensityGrid[row * nCellsX + column];

        // X Neighbour contribution
        if (xNeighbour >= 0 && xNeighbour < nCellsX)
        {
            agentDensity += Unity.Mathematics.math.abs(neighbourXWeight) * globalDensityGrid[row * nCellsX + xNeighbour];
        }

        // Z Neighbour contribution
        if (zNeighbour >= 0 && zNeighbour < nCellsZ)
        {
            agentDensity += Unity.Mathematics.math.abs(neighbourZWeight) * globalDensityGrid[zNeighbour * nCellsX + column];
        }

        // XZ Diagonal Neighbour contribution
        if (zNeighbour >= 0 && zNeighbour < nCellsZ && xNeighbour >= 0 && xNeighbour < nCellsX)
        {
            agentDensity += Unity.Mathematics.math.abs(neighbourXZWeight) * globalDensityGrid[zNeighbour * nCellsX + xNeighbour];
        }

        // Write the calculated result directly into our output array
        outDensityAtAgentPosition[index] = agentDensity;
    }
}