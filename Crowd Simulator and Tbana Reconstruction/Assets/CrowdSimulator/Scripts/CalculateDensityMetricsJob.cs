using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct CalculateMetricsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> agentPositions;
    [ReadOnly] public NativeMultiHashMap<int, int> neighMatrix;
    [ReadOnly] public NativeArray<float> availableAreaGrid;
    [ReadOnly] public NativeArray<float3> train1Doors;
    [ReadOnly] public NativeArray<float3> train2Doors;

    public int nCellsX;
    public int nCellsZ;
    public float cellSize;
    public int nNeighbourBins;
    public float lenOfBin;
    public float3 xMinMax;
    public float3 zMinMax;

    public int platformType; // (0 = Central, 1 = Mixed, 2 = Side)
    public bool isDwellingT1;
    public bool isDwellingT2;
    public float halfDoorWidth;

    [WriteOnly] public NativeArray<float> outEntityDensity;
    [WriteOnly] public NativeArray<int> outSocialProximity;

    public void Execute(int agentIndex)
    {
        float3 agentPosition = agentPositions[agentIndex];

        // Get current agent's neighbor bin
        int row = (int)((agentPosition.z - zMinMax.x) / lenOfBin); 
		int column = (int)((agentPosition.x - xMinMax.x) / lenOfBin); 
		row = math.clamp(row, 0, nNeighbourBins - 1);
		column = math.clamp(column, 0, nNeighbourBins - 1);

        // Count agents within 1.5 and 1 meter
        int nAgents1_5m = 0;
        int nAgents1m = 0;
        int nBins = (int)math.ceil(1.5f / lenOfBin); // Number of bins per 1.5 m

        for (int rOffset = -nBins; rOffset <= nBins; rOffset++)
        {
            for (int cOffset = -nBins; cOffset <= nBins; cOffset++)
            {
                int currentRow = row + rOffset;
                int currentCol = column + cOffset;

                if (currentRow >= 0 && currentRow < nNeighbourBins && currentCol >= 0 && currentCol < nNeighbourBins)
                {
                    int binKey = currentRow * nNeighbourBins + currentCol;

                    if (neighMatrix.TryGetFirstValue(binKey, out int otherAgentIndex, out var iterator))
                    {
                        do
                        {
                            float3 otherAgentPosition = agentPositions[otherAgentIndex];
                            float distance = math.distance(agentPosition, otherAgentPosition);

                            if (distance <= 1.5f)
                            {
                                nAgents1_5m++;
                            }
                            if(distance <= 1f && agentIndex != otherAgentIndex)
                            {
                                nAgents1m++;
                            }
                        } while (neighMatrix.TryGetNextValue(out otherAgentIndex, ref iterator));
                    }
                }
            }
        }

        // Calculate accessible space within the 1.5m radius circle
        float sumAvailability = 0f;
        int sampleCount = 0;
        float spacing = 0.4f;

        for (float dx = -1.5f; dx <= 1.5f; dx += spacing)
        {
            for (float dz = -1.5f; dz <= 1.5f; dz += spacing)
            {
                if (dx * dx + dz * dz <= 1.5f * 1.5f)
                {
                    float3 samplePosition = agentPosition + new float3(dx, 0f, dz);

                    if (IsPointWalkable(samplePosition))
                    {
                        int sampleRow = (int)((samplePosition.z - zMinMax.x) / cellSize);
                        int sampleColumn = (int)((samplePosition.x - xMinMax.x) / cellSize);

                        if (sampleRow >= 0 && sampleRow < nCellsZ && sampleColumn >= 0 && sampleColumn < nCellsX)
                        {
                            sumAvailability += availableAreaGrid[sampleRow * nCellsX + sampleColumn];
                        }
                    }
                    sampleCount++;
                }
            }
        }

        float availabilityFraction = sumAvailability / sampleCount;
        float totalCircleArea = 7.0685834f; // pi * 1.5^2
        float accessibleSpace = totalCircleArea * availabilityFraction;

        if (accessibleSpace < 0.1f) accessibleSpace = 0.1f;

        outEntityDensity[agentIndex] = (float)nAgents1_5m / accessibleSpace;
        outSocialProximity[agentIndex] = nAgents1m;
    }

    private bool IsPointWalkable(float3 samplePosition)
    {
        float absX = math.abs(samplePosition.x);
        
        // Check if the point is on the platform
        bool onPlatform = false;
        if (platformType == 0) // Central
        {
            onPlatform = absX <= 9.0f;
        }      
        else if(platformType == 2) // Side
        {
            onPlatform = absX >= 3.0f;
        } 
        else if (platformType == 1) // Mixed
        {
            onPlatform = absX >= 6.0f || absX <= 3.0f;
        }
        
        if (onPlatform) return true;

        // If the point is outside the platform, it's walkable if
        // the train is dwelling and the point is near the train door
        if (isDwellingT1)
        {
            for (int i = 0; i < train1Doors.Length; i++)
            {
                if (math.abs(samplePosition.z - train1Doors[i].z) <= halfDoorWidth)
                {
                    return true;
                }
            }
        }
        
        if (isDwellingT2)
        {
            for (int i = 0; i < train2Doors.Length; i++)
            {
                if (math.abs(samplePosition.z - train2Doors[i].z) <= halfDoorWidth)
                {
                    return true;
                }
            }
        }

        // Otherwise it's not walkable
        return false;
    }
}
