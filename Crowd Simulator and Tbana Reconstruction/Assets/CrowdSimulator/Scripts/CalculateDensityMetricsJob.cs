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

        if(!IsPointOnThePlatform(agentPosition))
        {
            outEntityDensity[agentIndex] = -1.0f;
            outSocialProximity[agentIndex] = -1;
            return;
        }

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

                            if (distance <= 1.5f && IsPointOnThePlatform(otherAgentPosition))
                            {
                                nAgents1_5m++;
                            }
                            if(distance <= 1f && agentIndex != otherAgentIndex && IsPointOnThePlatform(otherAgentPosition))
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
        float spacing = 0.3f;

        for (float dx = -1.5f; dx <= 1.5f; dx += spacing)
        {
            for (float dz = -1.5f; dz <= 1.5f; dz += spacing)
            {
                if (dx * dx + dz * dz <= 1.5f * 1.5f)
                {
                    float3 samplePoint = agentPosition + new float3(dx, 0f, dz);

                    if (IsPointFree(samplePoint))
                    {
                        sumAvailability += 1.0f;
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

    private bool IsPointOnThePlatform(float3 point)
    {
        float absX = math.abs(point.x);
        
        // Check if the point is on the platform
        if (platformType == 0) // Central
        {   
            if(absX <= 9.0f) return true;
        }      
        else if(platformType == 2) // Side
        {   
            if(absX >= 3.0f) return true;
        } 
        else if (platformType == 1) // Mixed
        {
            if(absX >= 6.0f || absX <= 3.0f) return true;
        }
        return false;
    }

    private bool IsPointFree(float3 point)
    {
        float absX = math.abs(point.x);
        float absZ = math.abs(point.z);

        // Check platform bounds
        if(absX > 12f) return false; 
        if(absZ > 75f) return false;

        // Check train tracks
        if(platformType == 0) // Central
        {
            if(absX > 9f) return false; 
        }else if(platformType == 2) // Side
        {
            if(absX < 3f) return false;
        }else if(platformType == 1) // Mixed
        {
            if(absX > 3f && absX < 6f) return false;
        }

        // Check stairs
        if(platformType == 0) // Central
        {
            if(absX <= 3f && absZ >= 20f && absZ <= 50f) return false;  
        }else if(platformType == 2) // Side
        {
            if(absX >= 9f && absZ >= 20f && absZ <= 50f) return false;
        }else if(platformType == 1) // Mixed
        {
            if((absX <= 1f || absX >= 10f) && absZ >= 20f && absZ <= 50f) return false;
        }
        return true;
    }
}
