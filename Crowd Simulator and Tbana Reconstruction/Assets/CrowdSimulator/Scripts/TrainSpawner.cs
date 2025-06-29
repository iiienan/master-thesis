using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainSpawner : MonoBehaviour
{
    private int goal;
    private float burstRate;
    private GameObject agentContainer;
    private Main mainScript;
    private Agent agentPrefab;
    internal Material alightingAgentMaterial;
    internal bool done = false;
    private bool alightBeforeBoarding;
    private TrainController.PlatformType platformType;
    private Train train;

    // Start is called before the first frame update
    public void Initialize(Train train, int goal, float burstRate, GameObject agentContainer, Agent agentPrefab, Material alightingAgentMaterial, bool alightBeforeBoarding,
    TrainController.PlatformType platformType)
    {
        this.agentPrefab = agentPrefab;
        this.burstRate = burstRate;
        this.agentContainer = agentContainer;
        this.train = train;
        this.goal = goal;
        this.alightingAgentMaterial = alightingAgentMaterial;
        this.alightBeforeBoarding = alightBeforeBoarding;
        this.platformType = platformType;
        mainScript = FindObjectOfType<Main>();
    }

    public IEnumerator SpawnAgents()
    {
        while (train.nSpawnedAgents < train.numberOfAgents)
        {
            train.nSpawnedAgents++;
            spawnOneAgent();
            yield return new WaitForSeconds(burstRate + Random.Range(-0.1f, 0.2f));
        }
        done = true;
    }

    public void spawnOneAgent()
	{
        Vector3 startPosition = new Vector3(transform.position.x, 0f, transform.position.z + Random.Range(-0.5f, 0.5f));
		Agent agent;
		agent = Instantiate (agentPrefab);
        agent.GetComponentInChildren<Renderer>().material = alightingAgentMaterial;

        int node = transform.GetComponent<CustomNode>().index;

		agent.InitializeAgent (startPosition, node, goal, ref mainScript.roadmap);

        if(alightBeforeBoarding)
        {
            if(platformType == TrainController.PlatformType.Central)
            {
                if(startPosition.x > 0)
                {
                    agent.noMapGoal = new Vector3(transform.position.x - 4f, 0f, startPosition.z);
                }else
                {
                    agent.noMapGoal = new Vector3(transform.position.x + 4f, 0f, startPosition.z);
                }
                agent.noMap = true;
            }

            if(platformType == TrainController.PlatformType.Side)
            {
                if(startPosition.x > 0)
                {
                    agent.noMapGoal = new Vector3(transform.position.x + 4f, 0f, startPosition.z);
                }else
                {
                    agent.noMapGoal = new Vector3(transform.position.x - 4f, 0f, startPosition.z);
                }
                agent.noMap = true;
            }
        }
		
        agent.isAlighting = true;
		if (agentContainer != null)
			agent.transform.parent = agentContainer.transform;

		mainScript.agentList.Add (agent);
	}
}
