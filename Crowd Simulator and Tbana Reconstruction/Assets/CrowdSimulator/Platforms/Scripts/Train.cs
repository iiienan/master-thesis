using System.Collections.Generic;
using UnityEngine;

public class Train : MonoBehaviour
{
    public int trainLine;
    public int numberOfAgents;
    internal List<CarriageSpawner> trainSpawners;
    public List<CustomNode> goalNodes;
    public GameObject agentContainer;
    public Agent agentPrefab;
    public Material alightingAgentMaterial;
    internal int nSpawnedAgents = 0;
    [SerializeField] internal string doorsSide;

    // Start is called before the first frame update
    void Start()
    {
        if (goalNodes == null || goalNodes.Count == 0)
        {
            Debug.LogError("Goal nodes not set for train " + gameObject.name);
            return;
        }
        if (agentContainer == null)
        {
            Debug.LogError("Agent container not set for train " + gameObject.name);
            return;
        }

        trainSpawners = new List<CarriageSpawner>();
        Transform spawners = transform.Find("Spawners");

        foreach (Transform spawnerTransform in spawners)
        {
            CarriageSpawner spawner = spawnerTransform.GetComponent<CarriageSpawner>();
            if (spawner != null)
            {
                trainSpawners.Add(spawner);
            }
            else
            {
                Debug.LogError("CarriageSpawner component not found on " + spawnerTransform.name + " for train " + gameObject.name);
            }
        }

        TrainController trainController = FindObjectOfType<TrainController>();
        if (trainController == null)
        {
            Debug.LogError("TrainController not found in the scene.");
            return;
        }

        int agentsPerSpawner = numberOfAgents / trainSpawners.Count;
        int remainder = numberOfAgents % trainSpawners.Count;

        for (int i = 0; i < trainSpawners.Count; i++)
        {
            int agentsForThisSpawner = agentsPerSpawner + (i < remainder ? 1 : 0);

            if (agentsForThisSpawner > 0)
            {
                CarriageSpawner spawner = trainSpawners[i];
                

                if (alightingAgentMaterial == null)
                {
                    Debug.LogError("Alighting agent material not set for train " + gameObject.name);
                    return;
                }
                spawner.Initialize(this, agentContainer, agentPrefab, alightingAgentMaterial, agentsForThisSpawner);
            }
        }

    }

}
