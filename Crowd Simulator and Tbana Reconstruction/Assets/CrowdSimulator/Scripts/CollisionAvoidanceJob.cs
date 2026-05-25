using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct CollisionAvoidanceJob : IJobParallelFor
{
    // INPUT DATA (ReadOnly)
    [ReadOnly] public NativeArray<float3> agentPositions;
    [ReadOnly] public NativeArray<float3> preferredVelocities;
    [ReadOnly] public NativeArray<bool> isWaitingFlags;
    [ReadOnly] public NativeArray<bool> isPreparingFlags;
    [ReadOnly] public NativeArray<bool> doneFlags;
    [ReadOnly] public NativeArray<float> walkingSpeeds;

    [ReadOnly] public NativeMultiHashMap<int, int> spatialGrid;

    // System Parameters
    public float ringDiameter;
    public float lenOfBin;
    public int neighbourBins;
    public float3 xMinMax;
    public float3 zMinMax;

    // OUTPUT DATA
    [WriteOnly] public NativeArray<float3> outCollisionAvoidanceVelocity;

    public void Execute(int index)
    {
        bool isCurrentAgentWaiting = isWaitingFlags[index] || (isPreparingFlags[index] && doneFlags[index]);

        float3 posA = agentPositions[index];
        float speedA = walkingSpeeds[index];
        float3 totalForce = float3.zero;

        // Determine spatial grid bin and clamp to grid boundaries (matching original SimulationGrid)
        int currentBinRow = (int)((posA.z - zMinMax.x) / lenOfBin);
        int currentBinCol = (int)((posA.x - xMinMax.x) / lenOfBin);
        currentBinRow = math.clamp(currentBinRow, 0, neighbourBins - 1);
        currentBinCol = math.clamp(currentBinCol, 0, neighbourBins - 1);

        // Search 3x3 surrounding spatial bins
        for (int rOffset = -1; rOffset <= 1; rOffset++)
        {
            for (int cOffset = -1; cOffset <= 1; cOffset++)
            {
                int targetRow = currentBinRow + rOffset;
                int targetCol = currentBinCol + cOffset;

                if (targetRow >= 0 && targetRow < neighbourBins && targetCol >= 0 && targetCol < neighbourBins)
                {
                    int binKey = targetRow * neighbourBins + targetCol;

                    if (spatialGrid.TryGetFirstValue(binKey, out int otherAgentIndex, out var iterator))
                    {
                        do
                        {
                            if (index == otherAgentIndex) continue; // Skip self

                            float3 posB = agentPositions[otherAgentIndex];
                            float3 disVector = posA - posB; // Vector pointing from B to A
                            float distance = math.length(disVector);

                            if (distance <= 0.001f) continue;

                            // --- CONDITION 1: BOTH AGENTS ARE WAITING ---
                            if (isCurrentAgentWaiting && (isWaitingFlags[otherAgentIndex] || (isPreparingFlags[otherAgentIndex] && doneFlags[otherAgentIndex])))
                            {
                                float standingDistance = 0.4f;
                                if (distance < standingDistance)
                                {
                                    totalForce += math.normalize(disVector) * (standingDistance - distance) * speedA;
                                }
                                continue; 
                            }

                            // --- CONDITION 2 & 4: THE FLIPPED BUMP LOGIC ---
                            // If the current agent processing this thread IS waiting, it needs to see 
                            // if a moving agent (otherAgentIndex) is bumping into it.
                            if (isCurrentAgentWaiting)
                            {
                                bool isOtherAgentMoving = !isWaitingFlags[otherAgentIndex] && !(isPreparingFlags[otherAgentIndex] && doneFlags[otherAgentIndex]);
                                float bumpDiameter = 0.4f;

                                if (isOtherAgentMoving && distance <= bumpDiameter)
                                {
                                    // Use the moving agent's velocity and speed to calculate the shove
                                    float3 movingAgentPrefVel = preferredVelocities[otherAgentIndex];
                                    float lenSq = math.lengthsq(movingAgentPrefVel);

                                    if (lenSq > 0.0001f)
                                    {
                                        float movingAgentSpeed = walkingSpeeds[otherAgentIndex];
                                        float3 walkDir = movingAgentPrefVel / math.sqrt(lenSq);
                                        float3 sideDir = new float3(-walkDir.z, 0f, walkDir.x); // Perpendicular vector (Cross product with Vector3.up)

                                        // Decide left or right based on relative position from the moving agent's perspective
                                        float3 relative = posA - posB; // Position of waiting agent relative to moving agent
                                        // Standard Unity Mathf.Sign returns 1f for zero, so we use >= 0f comparison to avoid math.sign(0) returning 0f
                                        float sideSign = math.dot(relative, sideDir) >= 0f ? 1f : -1f;
                                        
                                        // Apply the side-stepping force to our current waiting agent!
                                        totalForce += sideDir * sideSign * movingAgentSpeed * 0.5f;
                                    }
                                }
                                continue;
                            }

                            // --- CONDITION 3: STANDARD RUNNING PUSH FORCE ---
                            // This only executes if the current agent is a MOVING agent.
                            if (distance < ringDiameter)
                            {
                                totalForce += math.normalize(disVector) * (ringDiameter - distance) * speedA;
                            }

                        } while (spatialGrid.TryGetNextValue(out otherAgentIndex, ref iterator));
                    }
                }
            }
        }

        outCollisionAvoidanceVelocity[index] = totalForce;
    }
}