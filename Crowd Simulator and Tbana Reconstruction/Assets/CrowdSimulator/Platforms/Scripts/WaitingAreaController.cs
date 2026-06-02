using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/*
*   This class manages all the waiting areas and related functions.
*/
public class WaitingAreaController : MonoBehaviour
{  
    public List<WaitingArea> waitingAreas;                          // All the waiting areas in the scene
    internal List<Agent> waitingAgents;                              // Agents that are currently waiting
    public Dictionary<int, List<int>> spawnerWaitingAreaDistances;  // The distance from each spawner to each waiting area in descending order
    public GameObject agentContainer;                               
    public float wdistance = 1.0f, wdensity = 1.0f, wtrainline = 1.0f, wpriority = 1.0f;
    public bool debug = false;
    public float waitingSpotSize = 0.5f;
    public bool useRowColumns = false;
    private TrainController trainController;
    public Material waitingAgentMaterial;
    public Material walkingAgentMaterial;
    public Material boardingAgentMaterial;
    private Main mainScript;
    public Train[] trains;
    private CustomNode[][] trainDoorNodes; 
    internal Vector3[][] trainDoorPositions;

    public void Initialize()
    {
        waitingAreas = new List<WaitingArea>();
        waitingAgents = new List<Agent>();

        mainScript = FindObjectOfType<Main>();
        if(mainScript == null)
        {
            Debug.LogError("Main script not found in the scene.");
            return;
        }
        trainController = FindObjectOfType<TrainController>();
        if(trainController == null)
        {
            Debug.LogError("TrainController not found in the scene.");
        }

        int totalWaitingSpots = 0;

        foreach(WaitingArea waitingArea in FindObjectsOfType<WaitingArea>())
        {
            waitingAreas.Add(waitingArea);
            totalWaitingSpots += waitingArea.NWaitingSpots(waitingSpotSize);
        }

        if(mainScript.testController.flowType == TrainController.Flow.Asymmetric && (
            trainController.platformType == TrainController.PlatformType.Side 
            || trainController.platformType == TrainController.PlatformType.Mixed))
        {
            if(totalWaitingSpots / 2f < mainScript.testController.entryFlowLines[0] || totalWaitingSpots / 2f < mainScript.testController.entryFlowLines[1])
            {
                Debug.Log("Warning: Total waiting spots (" + totalWaitingSpots/2f + ") is less than the entry flow for one line. Decreasing waiting spot size.");
                mainScript.logger.LogWarning("Total waiting spots (" + totalWaitingSpots/2f + ") is less than the entry flow for one line. Decreasing waiting spot size.");
                float totalAvailableArea = 0;

                foreach (WaitingArea area in waitingAreas)
                {
                    totalAvailableArea += area.GetArea();
                }

                float totalAreaPerPlatform = totalAvailableArea / 2f;
                int biggestFlow = Mathf.Max(mainScript.testController.entryFlowLines[0], mainScript.testController.entryFlowLines[1]);

                float idealWaitingSpotSize = Mathf.Sqrt(totalAreaPerPlatform / biggestFlow);
                waitingSpotSize = idealWaitingSpotSize * 0.90f;
            }
        }

        else if(totalWaitingSpots < mainScript.testController.entryFlow)
        {
            Debug.Log("Warning: Total waiting spots (" + totalWaitingSpots + ") is less than the entry flow (" + mainScript.testController.entryFlow + "). Decreasing waiting spot size.");
            mainScript.logger.LogWarning("Total waiting spots (" + totalWaitingSpots + ") is less than the entry flow (" + mainScript.testController.entryFlow + "). Decreasing waiting spot size.");
            float totalAvailableArea = 0;

            foreach (WaitingArea area in waitingAreas)
            {
                totalAvailableArea += area.GetArea();
            }

            float idealWaitingSpotSize = Mathf.Sqrt(totalAvailableArea / mainScript.testController.entryFlow);
            waitingSpotSize = idealWaitingSpotSize * 0.90f;
        }

        foreach(WaitingArea waitingArea in waitingAreas)
        {
            waitingArea.Initialize(debug, waitingSpotSize, useRowColumns);
        }

        BuildSpawnerWaitingAreaDistances();

        trainDoorNodes = new CustomNode[trains.Length][];
        for (int i = 0; i < trains.Length; i++)
        {
            GameObject trainDoors = trains[i].transform.Find("TrainSpawners").gameObject;
            trainDoorNodes[i] = trainDoors.GetComponentsInChildren<CustomNode>();
        }
        
        trainDoorPositions = new Vector3[trains.Length][];
        for (int i = 0; i < trains.Length; i++)
        {
            trainDoorPositions[i] = trainDoorNodes[i].Select(n => n.transform.position).ToArray();
        }
    }

    /*
    *   Get a waiting spot in the closest waiting area that has free spots.
    *   Returns the index of the waiting area in the roadmap and the position of the waiting spot.
    */
    public (int,int) GetWaitingAreaSpot(int startNode)
    {

        foreach (int waitingAreaIndex in spawnerWaitingAreaDistances[startNode])
        {
            (int waitingAreaMapIndex, int waitingAreaSpot) areaAndSpot = waitingAreas[waitingAreaIndex].GetWaitingSpot();
            if(areaAndSpot.waitingAreaSpot != -1)
            {
                return (areaAndSpot.waitingAreaMapIndex, areaAndSpot.waitingAreaSpot);
            }
        }
        return (-1,-1);
    }

    public (int,int) GetWaitingAreaSpotNew(CustomNode startNode, int trainLine, bool forceTrainLine = false)
    {
        float bestScore = Mathf.Infinity;
        WaitingArea bestWaitingArea = null;
        foreach (WaitingArea waitingArea in waitingAreas)
        {   
            if(!waitingArea.HasFreeWaitingSpots())
            {
                continue;
            }
            float score;
            float distance = Vector3.Distance(startNode.transform.position, waitingArea.transform.position);
            distance /= 150f; // Normalize the distance to a value between 0 and 1
            float density = waitingArea.GetDensity();
            int closestTrainLine;

            if(waitingArea.transform.position.x >= 0)
            {
                closestTrainLine = 1;
            }
            else
            {
                closestTrainLine = 2;
            }

            if(forceTrainLine && trainLine != closestTrainLine)
            {
                continue;
            }

            float lineMismatch;

            if(trainLine == closestTrainLine)
            {
                lineMismatch = 0f;
            }
            else
            {
                lineMismatch = 1f;
            }

            float priority = waitingArea.priority * 0.1f;

            score = wdistance * distance + wdensity * density + wtrainline * lineMismatch + wpriority * priority;

            if(score < bestScore)
            {
                bestScore = score;
                bestWaitingArea = waitingArea;
            }
        }
        if(bestWaitingArea == null)
        {
            return (-1,-1);
        }

        (int waitingAreaMapIndex, int waitingAreaSpot) areaAndSpot = bestWaitingArea.GetWaitingSpot();
        if(areaAndSpot.waitingAreaSpot != -1)
        {
            return (areaAndSpot.waitingAreaMapIndex, areaAndSpot.waitingAreaSpot);
        }

        return (-1,-1);
    }

    /*
    *   Order the waiting areas by distance from each spawner.
    */
    private void BuildSpawnerWaitingAreaDistances()
    {
        spawnerWaitingAreaDistances = new Dictionary<int, List<int>>();

        foreach(MapGen.spawnNode spawner in mainScript.roadmap.spawns)
        {
            List<(int index,float distance)> distances = new List<(int, float)>();
            for (int areaIndex = 0; areaIndex < waitingAreas.Count; areaIndex++)
            {
                float distance = Vector3.Distance(mainScript.roadmap.allNodes[spawner.node].transform.position, waitingAreas[areaIndex].transform.position);
                distances.Add((areaIndex, distance));
            }

            List<int> sortedWaitingAreaIndexes = distances.OrderBy(pair => pair.distance)
                                                            .Select(pair => pair.index)
                                                            .ToList();

            spawnerWaitingAreaDistances.Add(spawner.node, sortedWaitingAreaIndexes);
        }
    }

    /*
    *   After the agent reached the waiting area, they will walk to the waiting spot.
    */
    public void walkAgentToWaitingSpot(Agent agent)
    {
        agent.done = false;
        agent.noMap = true;
        Vector3 adjustedPosition = agent.waitingArea.waitingSpots[agent.waitingSpot];
        adjustedPosition.x += Random.Range(-0.3f, 0.3f);
        adjustedPosition.z += Random.Range(-0.3f, 0.3f);
        agent.noMapGoal = adjustedPosition;
    }

    /*
    *   After the agent reached their waiting spot, they will be teleported to the exact position of the waiting spot.
    *   The agent will be frozen in place and will be an obstacle for other agents.
    *   The closest train door (node) will be set as the agent's goal.
    */
    public void SetWaitingAgent(Agent agent)
    {
        waitingAgents.Add(agent);
        int closestTrainDoor = FindClosestTrainDoor(agent);
        agent.rotateAgent(mainScript.roadmap.allNodes[closestTrainDoor].transform.position);
        agent.setNewPath(agent.goal, closestTrainDoor, mainScript.roadmap);
        agent.agentRenderer.material = waitingAgentMaterial;
        agent.isWaiting = true;
        agent.waitingPosition = agent.tr.position;
    }

    internal int FindClosestTrainDoor(Agent agent)
    {   
        float closestDistance = Mathf.Infinity;
        Vector3 currentPosition = agent.tr.position;
        int index = -1;

        for (int i = 0; i < trainDoorPositions[agent.trainLine - 1].Length; i++)
        {   
            float distance = Vector3.Distance(currentPosition, trainDoorPositions[agent.trainLine - 1][i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                index = trainDoorNodes[agent.trainLine - 1][i].index;
            }
        }
        
        if(index == -1)
        {
            Debug.LogError("No train door found");
        }
        return index;
    }
        

}
