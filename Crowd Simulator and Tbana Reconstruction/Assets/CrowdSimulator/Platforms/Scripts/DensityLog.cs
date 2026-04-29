using UnityEngine;

public class DensityLog : MonoBehaviour
{
    private Main mainScript;
    private float timer = 0f;
    private bool log = false;

    // Start is called before the first frame update
    void Start()
    {
        mainScript = FindObjectOfType<Main>();
        if(mainScript == null)
        {
            Debug.LogError("Main script not found in the scene.");
            return;
        }
        TestController testController = FindObjectOfType<TestController>();
        if(testController == null)        {
            Debug.LogError("TestController not found in the scene.");
            return;
        }
        log = testController.log;
    }

    // Update is called once per frame
    public void UpdateDensityLog()
    {
        if(!log)
        {
            return;
        }
        timer -= SimulationGrid.instance.dt;
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
                    Vector3 agentPos = agent.tr.position;
                    if(agentPos.x >= 0f && agentPos.x <= 9f)
                    {
                        platform1Count++;
                    }
                    if(agentPos.x < 0f && agentPos.x >= -9f)
                    {
                        platform2Count++;
                    }
                }
                mainScript.logger.LogDensity(platform1Count + "," + platform2Count);
                break;
            case TrainController.PlatformType.Side:
                foreach(Agent agent in mainScript.agentList)
                {
                    Vector3 agentPos = agent.tr.position;
                    if(agentPos.x >= 3f)
                    {
                        platform1Count++;
                    }
                    if(agentPos.x <= -3f)
                    {
                        platform2Count++;
                    }
                }
                mainScript.logger.LogDensity(platform1Count + "," + platform2Count);
                break;
            case TrainController.PlatformType.Mixed:
                int middlePlatformCount = 0;
                foreach(Agent agent in mainScript.agentList)                {
                    Vector3 agentPos = agent.tr.position;
                    if(agentPos.x >= 6f)
                    {
                        platform1Count++;
                    }
                    if(agentPos.x <= -6f)
                    {
                        platform2Count++;
                    }
                    if(agentPos.x < 3f && agentPos.x > -3f)
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
