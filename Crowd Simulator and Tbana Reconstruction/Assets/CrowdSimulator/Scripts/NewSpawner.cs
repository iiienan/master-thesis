using UnityEngine;
using System.Collections;

public class NewSpawner : MonoBehaviour {

	internal int node;	//The node for this spawner
	protected Main mainScript;

	// Waiting agents
	internal WaitingAreaController waitingAreaController;
	internal MapGen.map map; //map of available spawns / goals
	Vector2 X, Z; //Information about plane sizes
	internal float agentAvoidanceRadius;

	public GameObject agentEditorContainer = null;
	public CustomNode customGoal = null;
	internal int goal;

	public float timeBetweenSpawns;
	public bool usePoisson = false;
    public Agent agentPrefab;
	internal TestController testController;
	private float nextSpawnTimer;
	private CustomNode spawnerNode;

	// Set the node index for this spawner's node
	public void SetNode(int node)
	{
		this.node = node;
	}

	/**
	* Set the goal for the agents of this spawner.
	* If there is no custom goal set in the editor, the goal will be the goal node with index 0.
	*/
	private void SetGoal()
	{
		if(customGoal == null)
		{
			Debug.LogWarning("No custom goal set for spawner " + gameObject.name + ", using default goal with index 0.");
			goal = 0;
		}
		else
		{
			goal = customGoal.index;
		}
	}

	public void InitializeSpawner(MapGen.map map, Vector2 X, Vector2 Z, float agentAvoidanceRadius) {
		this.map = map;
		this.X = X; this.Z = Z;
		this.agentAvoidanceRadius = agentAvoidanceRadius;
		SetGoal();
	}

	void Start()
	{
		mainScript = FindObjectOfType<Main>();
		if(mainScript == null)
		{
			Debug.LogError("Main script not found in the scene.");
			return;
		}
		waitingAreaController = FindObjectOfType<WaitingAreaController>();
		if(waitingAreaController == null)
		{
			Debug.LogError("WaitingAreaController not found in the scene.");
			return;
		}
		testController = FindObjectOfType<TestController>();
		if(testController == null)
		{
			Debug.LogError("TestController not found in the scene.");
			return;
		}

		if(mainScript == null)
		{
			Debug.LogError("Main script not found in the scene.");
			return;
		}
		if(waitingAreaController == null)
		{
			Debug.LogError("CustomNode not found in children of " + gameObject.name);
			return;
		}
		if(testController == null)
		{
			Debug.LogError("TestController not found in the scene.");
			return;
		}

		spawnerNode = GetComponentInChildren<CustomNode>();

		SetSpawnRate();
		nextSpawnTimer = 0f;
	}

	internal virtual void SetSpawnRate()
	{
		timeBetweenSpawns = 4f * testController.arriveInterval / testController.entryFlow;
	}

	// CONTINUOUS SPAWN
	public void UpdateSpawner()
	{
		if(mainScript.agentList.Count >= mainScript.maxNumberOfAgents)
		{
			return;
		}

		nextSpawnTimer -= SimulationGrid.instance.dt;

		if(nextSpawnTimer <= 0)
		{
			Vector3 startPos = new Vector3 (Random.Range (-0.5f, 0.5f), 0f, Random.Range (-0.5f, 0.5f)); 
			startPos = spawnerNode.transform.TransformPoint (startPos);
			SpawnOneAgent(startPos);

			if(usePoisson)
			{
				nextSpawnTimer = CalculateTimeBetweenSpawns();
			}
			else
			{
				nextSpawnTimer = timeBetweenSpawns;
			}
		}

		
	}

	// BURST SPAWN
	public IEnumerator BurstSpawn(int nAgents, float burstRate)
	{
		for (int i = 0; i < nAgents; ++i) {
			Vector3 startPos = new Vector3(transform.position.x + Random.Range(-1.5f, 1.5f), transform.position.y, transform.position.z + Random.Range(-1.5f, 1.5f));
			SpawnOneAgent (startPos);
			yield return new WaitForSeconds (burstRate);
		}

	}

	public void SpawnOneAgent(Vector3 startPosition)
	{
        Agent agent = Instantiate (agentPrefab);

		int agentGoal = SetSubwayData(agent, startPosition);
		agent.InitializeAgent (startPosition, node, agentGoal, map);

		if (agentEditorContainer != null)
			agent.tr.parent = agentEditorContainer.transform;

		mainScript.agentList.Add (agent);
		mainScript.nEnteringAgents++;
	}

	internal virtual int SetSubwayData(Agent agent, Vector3 startPosition)
	{
		int agentGoal = goal;
		int trainLine;
		if(testController.flowType == TrainController.Flow.Asymmetric && testController.scenario == TestController.Scenario.Entry)
		{
			float rand = Random.value;

			if (rand < 0.20f) 
			{
				trainLine = 2;  // Reduced flow
			} else 
			{
				trainLine = 1;  // Increased flow
			}
		}
		else
		{
			trainLine = Random.Range(1,3);
		}
		agent.trainLine = trainLine;

		// Find a waiting area goal for the agent. If there are no free waiting area spots their goal will be the ordinary goal for this spawner.
		(int waitingArea,int waitingSpot) waitingAreaSpot = waitingAreaController.GetWaitingAreaSpotNew(spawnerNode, trainLine);
		if(waitingAreaSpot.waitingArea != -1)
		{
			agent.setWaitingAgent(true);
			agentGoal = waitingAreaSpot.waitingArea;
			agent.waitingSpot = waitingAreaSpot.waitingSpot;
			agent.waitingArea = map.allNodes[waitingAreaSpot.waitingArea].GetComponent<WaitingArea>();
		}

		return agentGoal;
	}


	float CalculateTimeBetweenSpawns()
    {
		float spawnRate = 1f / timeBetweenSpawns;
        float u = Random.value;
        // -ln(1-u)/λ
        return -Mathf.Log(1 - u) / spawnRate;
    }
}
