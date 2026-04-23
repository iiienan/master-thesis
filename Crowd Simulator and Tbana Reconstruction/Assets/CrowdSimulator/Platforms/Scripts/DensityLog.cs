using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DensityLog : MonoBehaviour
{
    private Main mainScript;
    private float timer = 0f;

    // Start is called before the first frame update
    void Start()
    {
        mainScript = FindObjectOfType<Main>();
        if(mainScript == null)
        {
            Debug.LogError("Main script not found in the scene.");
            return;
        }
    }

    // Update is called once per frame
    public void UpdateDensityLog()
    {
        timer -= Grid.instance.dt;
        if(timer <= 0f)
        {
            countAgents();
            timer += 1f;
        }
    }

    private void countAgents()
    {
        int platform1Count = 0;
        int platform2Count = 0;
        switch (mainScript.trainController.platformType)
        {
            case TrainController.PlatformType.Central:
                foreach(Agent agent in mainScript.agentList)
                {
                    if(agent.tr.position.x >= 0f && agent.tr.position.x <= 9f)
                    {
                        platform1Count++;
                    }
                    if(agent.tr.position.x < 0f && agent.tr.position.x >= -9f)
                    {
                        platform2Count++;
                    }
                }
                mainScript.logger.LogDensity(platform1Count + "," + platform2Count);
                break;
            case TrainController.PlatformType.Side:
                foreach(Agent agent in mainScript.agentList)
                {
                    if(agent.tr.position.x >= 3f)
                    {
                        platform1Count++;
                    }
                    if(agent.tr.position.x <= -3f)
                    {
                        platform2Count++;
                    }
                }
                mainScript.logger.LogDensity(platform1Count + "," + platform2Count);
                break;
            case TrainController.PlatformType.Mixed:
                int middlePlatformCount = 0;
                foreach(Agent agent in mainScript.agentList)                {
                    if(agent.tr.position.x >= 6f)
                    {
                        platform1Count++;
                    }
                    if(agent.tr.position.x <= -6f)
                    {
                        platform2Count++;
                    }
                    if(agent.tr.position.x < 3f && agent.tr.position.x > -3f)
                    {
                        middlePlatformCount++;
                    }
                }
                mainScript.logger.LogDensity(platform1Count + "," + platform2Count + "," + middlePlatformCount);
                break;
            default:
                Debug.LogError("Unknown platform type in DensityLog.");
                break;
        }
    }
}
