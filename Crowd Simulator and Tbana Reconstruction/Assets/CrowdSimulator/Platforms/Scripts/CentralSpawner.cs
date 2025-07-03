using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CentralSpawner : NewSpawner
{
    public int trainLine;

    internal override int SetSubwayData(Agent agent, Vector3 startPosition)
    {
        int agentGoal = goal;
        agent.trainLine = trainLine;

        // Find a waiting area goal for the agent. If there are no free waiting area spots their goal will be the ordinary goal for this spawner.
        CustomNode startNode = transform.GetChild(0).GetComponent<CustomNode>();
        (int waitingArea, int waitingSpot) waitingAreaSpot = waitingAreaController.GetWaitingAreaSpotNew(ref startNode, trainLine, true);
        if (waitingAreaSpot.waitingArea != -1)
        {
            agent.setWaitingAgent(true);
            agentGoal = waitingAreaSpot.waitingArea;
            agent.waitingSpot = waitingAreaSpot.waitingSpot;
            agent.waitingArea = map.allNodes[waitingAreaSpot.waitingArea].GetComponent<WaitingArea>();
        }
        return agentGoal;

    }

    internal override void SetSpawnRate()
    {
        if (testController.flowType == TrainController.Flow.Asymmetric && testController.scenario == TestController.Scenario.Entry)
        {
            float totalSpawnRate = testController.entryFlow / testController.arriveInterval;
            if (trainLine == 1)
            {
                spawnRate = totalSpawnRate / 5f;
            }
            else if (trainLine == 2)
            {
                spawnRate = totalSpawnRate / 20f;
            }
            else
            {
                Debug.LogError("Invalid train line specified for spawner");
            }
        }
        else
        {
            spawnRate = testController.entryFlow / 8f / testController.arriveInterval;
        }
    }


}
