using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SubwaySpawner : MonoBehaviour {

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
    public int trainLine;
	private List<Collider> doorColliders = new List<Collider>();

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
			//Debug.LogWarning("No custom goal set for spawner " + gameObject.name + ", using default goal with index 0.");
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

		if(mainScript.trainController.platformType != TrainController.PlatformType.Mixed) return;
		

		Transform trainDoorsR = mainScript.trainController.trains[0].transform.Find("TrainDoorsR");
		if (trainDoorsR != null)
		{
			foreach (Transform door in trainDoorsR)
			{
				door.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
				Collider doorCollider = door.GetComponent<Collider>();
				doorColliders.Add(doorCollider);
			}
		}
		else
		{
			Debug.LogError("Train doors not found for train 1");
		}

		Transform trainDoorsL = mainScript.trainController.trains[1].transform.Find("TrainDoorsL");
		if (trainDoorsL != null)
		{
			foreach (Transform door in trainDoorsL)
			{
				door.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
				Collider doorCollider = door.GetComponent<Collider>();
				doorColliders.Add(doorCollider);
			}
		}
		else
		{
			Debug.LogError("Train doors not found for train 2");
		}	
	}

	internal void SetSpawnRate()
    {
        if (testController.flowType == TrainController.Flow.Asymmetric && testController.scenario == TestController.Scenario.Entry)
        {
            float totalSpawnRate = testController.entryFlow / testController.arriveInterval;
            if (trainLine == 1)
            {
                timeBetweenSpawns = 1f / (totalSpawnRate / 5f);
            }
            else if (trainLine == 2)
            {
                timeBetweenSpawns = 1f / (totalSpawnRate / 20f);
            }
            else
            {
                Debug.LogError("Invalid train line specified for spawner");
            }
        }
        else
        {
            timeBetweenSpawns = 8f * testController.arriveInterval / testController.entryFlow;
        }
    }

	// CONTINUOUS SPAWN
	public void UpdateSpawner()
	{
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
				nextSpawnTimer += timeBetweenSpawns;
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
		mainScript.nEnteringAgents[agent.trainLine - 1]++;

		if(mainScript.trainController.platformType == TrainController.PlatformType.Mixed)
		{
			Collider agentCollider = agent.GetComponent<Collider>();
			
			foreach (Collider doorCollider in doorColliders)
			{
				Physics.IgnoreCollision(agentCollider, doorCollider);
			}
		}
	}

	internal int SetSubwayData(Agent agent, Vector3 startPosition)
    {
        int agentGoal = goal;
        agent.trainLine = trainLine;
        agent.agentType = TrainController.AgentType.Boarding;

        CustomNode startNode = transform.GetChild(0).GetComponent<CustomNode>();
        (int waitingArea, int waitingSpot) waitingAreaSpot;
        if(mainScript.trainController.platformType == TrainController.PlatformType.Central)
		{
			waitingAreaSpot = waitingAreaController.GetWaitingAreaSpotNew(startNode, trainLine, false);
		}
		else
		{
			waitingAreaSpot = waitingAreaController.GetWaitingAreaSpotNew(startNode, trainLine, true);
		}
		
        if (waitingAreaSpot.waitingArea != -1)
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
