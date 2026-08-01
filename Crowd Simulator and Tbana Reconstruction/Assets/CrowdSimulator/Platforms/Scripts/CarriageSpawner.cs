using System.Collections.Generic;
using UnityEngine;

public class CarriageSpawner : MonoBehaviour
{
    private GameObject agentContainer;
    private Main mainScript;
    private Agent agentPrefab;
    internal Material alightingAgentMaterial;
    private Train train;
    private int nAgentsToSpawn;
    private List<TrainNode> spawnerNodes;
    const int N_DOORS = 4;
    const int SPAWN_AREA_X = 1;
    const int SPAWN_AREA_Z = 2;
    private int[] closestGoals = new int[N_DOORS];
    private float spawnInterval = 0.1f;
    private float timeSinceLastSpawn = 0f;
    internal int id;

    // Start is called before the first frame update
    public void Initialize(Train train, GameObject agentContainer, Agent agentPrefab, Material alightingAgentMaterial, int nAgentsToSpawn, int id)
    {
        this.agentPrefab = agentPrefab;
        this.agentContainer = agentContainer;
        this.train = train;
        this.nAgentsToSpawn = nAgentsToSpawn;
        this.id = id;
        this.alightingAgentMaterial = alightingAgentMaterial;
        mainScript = FindObjectOfType<Main>();
        if (mainScript == null)        {
            Debug.LogError("Main script not found in the scene.");
            return;
        }
        if(alightingAgentMaterial == null)
        {
            Debug.LogError("Alighting agent material not set for train " + train.gameObject.name);
            return;
        }
        spawnerNodes = new List<TrainNode>();
        foreach(Transform child in transform)
        {
            TrainNode node = child.GetComponent<TrainNode>();
            if(node != null)
            {
                spawnerNodes.Add(node);
                //node.trainCar = id;
            }
        }
        if(spawnerNodes.Count == 0)
        {
            Debug.LogError("No spawner nodes found for train " + train.gameObject.name);
            return;
        }
        for (int i = 0; i < spawnerNodes.Count; i++)
        {
            closestGoals[i] = FindClosestGoal(spawnerNodes[i].transform.position);
        }
    }

    public int SpawnAgents()
    {
        int agentsPerDoor = nAgentsToSpawn / N_DOORS;
        int remainder = nAgentsToSpawn % N_DOORS;

        for (int i = 0; i < N_DOORS; i++)
        {
            int agentsForThisNode = agentsPerDoor + (i < remainder ? 1 : 0);

            if (agentsForThisNode > 0)
            {
                SpawnAgentsInGrid(i, agentsForThisNode);
            }
        }
        return nAgentsToSpawn;
    }

    private void SpawnAgentsInGrid(int spawnerIndex, int agentsForThisNode)
    {
        float minX = -SPAWN_AREA_X, maxX = SPAWN_AREA_X;
        float minZ = -SPAWN_AREA_Z, maxZ = SPAWN_AREA_Z;

        float totalWidth = maxX - minX;
        float totalHeight = maxZ - minZ;

        int columns = Mathf.CeilToInt(Mathf.Sqrt(agentsForThisNode*0.5f));
        columns = Mathf.Clamp(columns, 1, agentsForThisNode);
        int rows = Mathf.CeilToInt((float)agentsForThisNode / columns);

        float stepX = columns > 1 ? totalWidth / (columns - 1) : 0f;
        float stepZ = rows > 1 ? totalHeight / (rows - 1) : 0f;

        int nAgentsSpawned = 0;

        Vector3 spawnerPosition = spawnerNodes[spawnerIndex].transform.position;

        float startX = columns > 1 ? (spawnerPosition.x + minX) : spawnerPosition.x;
        float startZ = rows > 1 ? (spawnerPosition.z + minZ) : spawnerPosition.z;

        for(int r = 0; r < rows; r++)
        {
            for(int c = 0; c < columns; c++)
            {

                if(nAgentsSpawned >= agentsForThisNode) return;

                float xPos = startX + stepX * c;
                float zPos = startZ + stepZ * r;
                Vector3 startPosition = new Vector3(xPos, 0f, zPos);

                SpawnOneAgent(spawnerNodes[spawnerIndex].index, closestGoals[spawnerIndex], spawnerNodes[spawnerIndex].trainCar, startPosition);
                nAgentsSpawned++;
                TrainController.instance.nAgentsInCarriage[train.trainLine-1, spawnerNodes[spawnerIndex].trainCar]++;
            }
        }
    }

    public int UpdateSpawner(int nAgentsInsideTrain, int nAgentsToSpawn = N_DOORS)
    {
        if (nAgentsInsideTrain >= 2000) 
        {
            timeSinceLastSpawn = spawnInterval;
            return 0;
        }
        timeSinceLastSpawn += SimulationGrid.instance.dt;
        if (timeSinceLastSpawn < spawnInterval) return 0;
        timeSinceLastSpawn -= spawnInterval;

        for(int i = 0; i < nAgentsToSpawn; i++)
        {
            SpawnOneAgent(spawnerNodes[i].index, closestGoals[i], spawnerNodes[i].trainCar, spawnerNodes[i].transform.position);
            TrainController.instance.nAgentsInCarriage[train.trainLine-1, spawnerNodes[i].trainCar]++;
        }
        return nAgentsToSpawn;
    }

    public void SpawnOneAgent(int nodeIndex, int goal, int trainCar, Vector3? customPosition = null)
	{
        Vector3 startPosition = customPosition ?? new Vector3(transform.position.x, 0f, transform.position.z);
		Agent agent = Instantiate (agentPrefab);

        int node = nodeIndex;
        agent.agentType = TrainController.AgentType.Alighting;
		agent.InitializeAgent (startPosition, node, goal, mainScript.roadmap);
        agent.agentRenderer.material = alightingAgentMaterial;
        agent.trainLine = train.trainLine;
        agent.trainCar = trainCar;

        agent.crossingYellowLine = true;
		if (agentContainer != null)
            agent.tr.parent = agentContainer.transform;

		mainScript.agentList.Add (agent);
	}

    private int FindClosestGoal(Vector3 position)
    {
        int closestGoal = -1;
        float closestDistance = Mathf.Infinity;

        foreach (var goalNode in train.goalNodes)
        {
            float distance = Vector3.Distance(position, goalNode.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestGoal = goalNode.index;
            }
        }

        return closestGoal;
    }
}
