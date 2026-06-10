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

    private float nextSpawnTimer = 0f;
    internal bool isSpawning = false;
    private int nodeIndex;
    private bool waitOutsideTrain = false;

    // Start is called before the first frame update
    public void Initialize(Train train, int goal, float burstRate, GameObject agentContainer, Agent agentPrefab, Material alightingAgentMaterial, bool alightBeforeBoarding,
    TrainController.PlatformType platformType, bool waitOutsideTrain)
    {
        this.agentPrefab = agentPrefab;
        this.burstRate = burstRate;
        this.agentContainer = agentContainer;
        this.train = train;
        this.goal = goal;
        this.alightingAgentMaterial = alightingAgentMaterial;
        this.alightBeforeBoarding = alightBeforeBoarding;
        this.platformType = platformType;
        this.waitOutsideTrain = waitOutsideTrain;
        mainScript = FindObjectOfType<Main>();
        if (mainScript == null)        {
            Debug.LogError("Main script not found in the scene.");
            return;
        }
        nextSpawnTimer = burstRate + Random.Range(-0.1f, 0.1f);
        nodeIndex = GetComponent<CustomNode>().index;
        if(alightingAgentMaterial == null)
        {
            Debug.LogError("Alighting agent material not set for train " + train.gameObject.name);
            return;
        }
    }

    public void UpdateSpawner()
    {
        if (!isSpawning || done) return;

        if (train.nSpawnedAgents >= train.numberOfAgents)
        {
            isSpawning = false;
            done = true;
            return;
        }

        nextSpawnTimer -= SimulationGrid.instance.dt; 

        if (nextSpawnTimer <= 0)
        {
            spawnOneAgent();
            train.nSpawnedAgents++;
            nextSpawnTimer = burstRate + Random.Range(-0.1f, 0.1f);
        }
    }

    public void spawnOneAgent()
	{
        Vector3 startPosition = new Vector3(transform.position.x, 0f, transform.position.z + Random.Range(-0.5f, 0.5f));
		Agent agent = Instantiate (agentPrefab);

        int node = nodeIndex;

		agent.InitializeAgent (startPosition, node, goal, mainScript.roadmap);
        agent.agentRenderer.material = alightingAgentMaterial;
        agent.trainLine = train.trainLine;
        agent.agentType = TrainController.AgentType.Alighting;
        agent.pathIndex = 0;

        if (alightBeforeBoarding && waitOutsideTrain)
        {
            if (platformType == TrainController.PlatformType.Central)
            {
                if (startPosition.x > 0)
                {
                    agent.noMapGoal = new Vector3(transform.position.x - 4f, 0f, startPosition.z);
                }
                else
                {
                    agent.noMapGoal = new Vector3(transform.position.x + 4f, 0f, startPosition.z);
                }
                agent.noMap = true;
            }

            if (platformType == TrainController.PlatformType.Side)
            {
                if (startPosition.x > 0)
                {
                    agent.noMapGoal = new Vector3(transform.position.x + 4f, 0f, startPosition.z);
                }
                else
                {
                    agent.noMapGoal = new Vector3(transform.position.x - 4f, 0f, startPosition.z);
                }
                agent.noMap = true;
            }
        }

        agent.crossingYellowLine = true;
		if (agentContainer != null)
            agent.tr.parent = agentContainer.transform;

		mainScript.agentList.Add (agent);
	}
}
