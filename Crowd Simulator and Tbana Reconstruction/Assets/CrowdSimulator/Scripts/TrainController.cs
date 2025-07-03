using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrainController : MonoBehaviour
{
    public bool spawnTrains = true;
    public GameObject[] trains = new GameObject[3];
    private float arrivalTimer = 0f;
    internal float arriveInterval = 10f;
    internal bool[] dwelling = new bool[3];
    //public float dwellTime = 5f;
    //private float[] dwellTimer = new float[3];
    private WaitingAreaController waitingAreaController;
    internal Main mainScript;
    //public int trainCapacity = 500;
    internal int[] nBoardedAgents = new int[3];
    internal int nAgents = 100;
    public bool waitForMinimumAgents = false;
    //public bool boardWithCapacity = false;
    internal bool[] isPreparingToBoard = new bool[3];
    internal bool[] boarding = new bool[3];
    internal bool alightBeforeBoarding = true;
    public enum PlatformType
    {
        Central,
        Mixed,
        Side
    }

    public PlatformType platformType;

    public enum Flow
    {
        Symmetric,
        Asymmetric
    }

    internal Flow flow;
    internal int[] nBoardingAgents = new int[3];
    //public bool useDwellTimer = true;
    private Logger logger;
    private TestController testController;
    private bool[] allSpawnersDone = new bool[3];



    void Start()
    {
        waitingAreaController = FindObjectOfType<WaitingAreaController>();
        if (waitingAreaController == null)
        {
            Debug.LogError("WaitingAreaController not found");
        }
        mainScript = FindObjectOfType<Main>();
        if (mainScript == null)
        {
            Debug.LogError("Main not found");
        }
        testController = FindObjectOfType<TestController>();
        if (testController == null)
        {
            Debug.LogError("TestController not found");
        }
        if (testController.log) logger = FindObjectOfType<Logger>();
        if (logger == null && testController.log)
        {
            Debug.LogError("Logger not found");
        }

        Debug.Log("Test Case: " + platformType + " " + testController.scenario + " " + flow + " AB: " + alightBeforeBoarding.ToString());

        Debug.Log("Number of entering agents: " + nAgents);

        Debug.Log("Number of exiting agents: Train 1: " + trains[1].GetComponent<Train>().numberOfAgents + " Train 2: " + trains[2].GetComponent<Train>().numberOfAgents);


        ToggleTrain(1);
        ToggleTrain(2);
    }

    void Update()
    {
        if (!spawnTrains)
        {
            return;
        }
        if(!(dwelling[1] || dwelling[2]))
        {
            arrivalTimer += Time.deltaTime;
        }
        if (arrivalTimer >= arriveInterval)
        {
            if (waitForMinimumAgents && mainScript.agentList.Count < nAgents)
            {
                return;
            }
            arrivalTimer = 0f;
            //UnityEditor.EditorApplication.isPaused = true;
            dwelling[1] = true;
            dwelling[2] = true;

            ToggleTrain(1);
            ToggleTrain(2);
            if(logger != null) logger.LogEvent("Train 1 arrived");
            if(logger != null) logger.LogEvent("Train 2 arrived");

            PrepareBoarding(1);
            PrepareBoarding(2);

            StartCoroutine(Alight(1));
            StartCoroutine(Alight(2));
        }
        Dwell(1);
        Dwell(2);
        
    }

    private void ToggleTrain(int trainLine)
    {
        trains[trainLine].transform.GetChild(0).gameObject.SetActive(dwelling[trainLine]);
        trains[trainLine].transform.GetChild(1).gameObject.SetActive(dwelling[trainLine]);
        trains[trainLine].transform.GetChild(2).gameObject.SetActive(dwelling[trainLine]);
    }

    private IEnumerator Alight(int trainLine)
    {
        foreach (MapGen.spawnNode node in mainScript.roadmap.spawns)
        {
            node.spawner.spawn = false;
        }
        yield return new WaitForSeconds(10f);
        Train trainScript = trains[trainLine].GetComponent<Train>();
        if (logger != null) logger.LogEvent("Train " + trainLine + " started alighting");
        trainScript.Alight();
        Train train = trains[trainLine].GetComponent<Train>();


        if (!alightBeforeBoarding)
        {
            isPreparingToBoard[trainLine] = false;
            Board(trainLine);
        }

        allSpawnersDone[trainLine] = false;
        while (!allSpawnersDone[trainLine])
        {
            allSpawnersDone[trainLine] = true;
            foreach (var spawner in trainScript.trainSpawners)
            {
                if (!spawner.done)
                {
                    allSpawnersDone[trainLine] = false;
                    break;
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
        if (logger != null) logger.LogEvent("Train " + trainLine + " finished alighting");
        trains[trainLine].GetComponent<Train>().nSpawnedAgents = 0;

        if (alightBeforeBoarding)
        {
            isPreparingToBoard[trainLine] = false;
            Board(trainLine);
        }

    }

    public void PrepareBoarding(int trainLine)
    {
        isPreparingToBoard[trainLine] = true;
        PrepareWaitingAgents(trainLine);
        PrepareWalkingAgents(trainLine);
        nBoardedAgents[trainLine] = 0;
    }


    private void Dwell(int trainLine)
    {
        /**
        if (dwelling[trainLine] && useDwellTimer)
        {
            dwellTimer[trainLine] += Time.deltaTime;
            if (dwellTimer[trainLine] >= dwellTime)
            {
                dwelling[trainLine] = false;
                ToggleTrain(trainLine);
                dwellTimer[trainLine] = 0f;
                ResetLostAgents();
                boarding[trainLine] = false;
            }
        }
        **/
        if (dwelling[trainLine] && nBoardingAgents[trainLine] <= 0 && boarding[trainLine]
        && allSpawnersDone[trainLine])
        {
            //dwellTimer[trainLine] = 0f;
            boarding[trainLine] = false;
            if (logger != null) logger.LogEvent("Train " + trainLine + " finished boarding");
            StartCoroutine(TrainExit(trainLine));
        }
        
    }

    private IEnumerator TrainExit(int trainLine)
    {
        yield return new WaitForSeconds(5f);
        dwelling[trainLine] = false;
        ToggleTrain(trainLine);
        if (mainScript.nExitingAgents <= 0 && !dwelling[1] && !dwelling[2])
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }

    public void PrepareWaitingAgents(int trainLine)
    {
        for (int i = 0; i < waitingAreaController.waitingAgents.Count; i++)
        {
            Agent agent = waitingAreaController.waitingAgents[i];
            if (agent.trainLine == trainLine)
            {
                agent.GetComponentInChildren<Renderer>().material = waitingAreaController.boardingAgentMaterial;

                agent.isWaitingAgent = false;
                agent.waitingArea.isOccupied[agent.waitingSpot] = false;
                agent.waitingArea.freeWaitingSpots.Add(agent.waitingSpot);

                Rigidbody rb = agent.GetComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.None;

                StartCoroutine(WaitOutsideTrain(agent));

                nBoardedAgents[trainLine]++;
                /**
                if (nBoardedAgents[trainLine] >= trainCapacity && boardWithCapacity)
                {
                    break;
                }
                **/
            }
        }
    }

    private void PrepareWalkingAgents(int trainLine)
    {
        for(int i = 0; i < mainScript.agentList.Count; i++)
		{
			Agent agent = mainScript.agentList[i];
            if (agent.trainLine == trainLine && !agent.boarding && !agent.isWaiting)
            {
                PrepareWalkingAgent(agent);
                nBoardedAgents[trainLine]++;
                /**
                if (nBoardedAgents[trainLine] >= trainCapacity && boardWithCapacity)
                {
                    break;
                }
                **/
			}
            
		}
    }

    internal void PrepareWalkingAgent(Agent agent)
    {
        agent.GetComponentInChildren<Renderer>().material = waitingAreaController.boardingAgentMaterial;

        int closestTrainDoor = waitingAreaController.FindClosestTrainDoor(ref agent);
        int closestNode = FindClosestNode(agent.transform.position);
        agent.setNewPath(closestNode, closestTrainDoor, ref mainScript.roadmap);

        if(agent.isWaitingAgent)
        {
            agent.waitingArea.isOccupied[agent.waitingSpot] = false;
            agent.waitingArea.freeWaitingSpots.Add(agent.waitingSpot);
            agent.isWaitingAgent = false;
        }
        StartCoroutine(WaitOutsideTrain(agent));
    }

    private IEnumerator WaitOutsideTrain(Agent agent)
    {
        if (!agent) yield break;
        
        // Wait outside the train close to the door
        Vector3 targetPoint = mainScript.roadmap.allNodes[agent.path[agent.pathIndex]].transform.position;
        Vector3 waitPosition;
        if (agent.transform.position.z < targetPoint.z)
        {
            waitPosition = new Vector3(targetPoint.x, 0, targetPoint.z - Random.Range(1.5f, 2.5f));
        }
        else
        {
            waitPosition = new Vector3(targetPoint.x, 0, targetPoint.z + Random.Range(1.5f, 2.5f));
        }

        if (agent.transform.position.x < targetPoint.x)
        {
            waitPosition.x = targetPoint.x + Random.Range(-5f, 0.4f);
        }
        else
        {
            waitPosition.x = targetPoint.x + Random.Range(-0.4f, 5f);
        }

        if (platformType == PlatformType.Central)
        {
            waitPosition.x = Mathf.Clamp(waitPosition.x, -8.5f, 8.5f);
        }
        else if (platformType == PlatformType.Mixed)
        {
            if (agent.transform.position.x < targetPoint.x)
            {
                waitPosition.x = Mathf.Clamp(waitPosition.x, -9.5f, -6.5f);
            }
            else
            {
                waitPosition.x = Mathf.Clamp(waitPosition.x, 6.5f, 9.5f);
            }
        }
        else if (platformType == PlatformType.Side)
        {
            if (agent.transform.position.x < targetPoint.x)
            {
                waitPosition.x = Mathf.Clamp(waitPosition.x, -8.5f, -3.5f);
            }
            else
            {
                waitPosition.x = Mathf.Clamp(waitPosition.x, 3.5f, 8.5f);
            }
        }

        agent.noMapGoal = waitPosition;
        agent.noMap = true;

        // Reset these values to avoid unexpected movement
        agent.velocity = Vector3.zero;
        agent.preferredVelocity = Vector3.zero;
        agent.continuumVelocity = Vector3.zero;
        agent.collisionAvoidanceVelocity = Vector3.zero;

        // Start moving towards the train door
        agent.walkingSpeed = Random.Range(0.5f, 1f);
        agent.done = false;
        agent.isWaiting = false;
        agent.isPreparingToBoard = true;
        
        // Wait for a bit so all start moving
        // at exactly the same time
        float delay = Random.Range(0.1f, 3f);
        yield return new WaitForSeconds(delay);
    }

    public void Board(int trainLine)
    {
        if(logger != null) logger.LogEvent("Train " + trainLine + " started boarding");
        boarding[trainLine] = true;
        for (int i = mainScript.agentList.Count - 1; i >= 0; i--)
        {
            Agent agent = mainScript.agentList[i];
            if (agent.trainLine == trainLine)
            {
                if (!agent.isPreparingToBoard)
                {
                    continue;
                }
                StartCoroutine(BoardAgent(agent));
                nBoardingAgents[agent.trainLine]++;
            }
        }
    }

    internal IEnumerator BoardAgent(Agent agent)
    {
        agent.Reset();
        float delay = Random.Range(0.1f, 1f);
        yield return new WaitForSeconds(delay);

        if (!agent) yield break;

        waitingAreaController.waitingAgents.Remove(agent);

        agent.velocity = Vector3.zero;
        agent.preferredVelocity = Vector3.zero;
        agent.continuumVelocity = Vector3.zero;
        agent.collisionAvoidanceVelocity = Vector3.zero;

        if (waitingAreaController.agentContainer != null)
        {
            agent.transform.SetParent(waitingAreaController.agentContainer.transform);
        }
        else
        {
            agent.transform.SetParent(null);
        }


        agent.noMap = false;
        agent.done = false;
        agent.isWaiting = false;
        agent.isPreparingToBoard = false;
        agent.boarding = true;
        
    }

    int FindClosestNode(Vector3 position)
    {
        int closestNode = -1;
        float closestDistance = Mathf.Infinity;

        int layersToIgnore = LayerMask.GetMask("WaitingAgent", "Agent");
        int layerMask = ~layersToIgnore; // ignore these layers

        for (int j = 0; j < mainScript.roadmap.allNodes.Count; ++j) {
            Vector3 nodePos = mainScript.roadmap.allNodes[j].transform.position;
            float distance = (nodePos - position).magnitude;

            if (!Physics.Raycast(position, (nodePos - position).normalized, distance, layerMask)) {
                if (nodePos != transform.position && distance < closestDistance) {
                    closestDistance = distance;
                    closestNode = j;
                }
            }
        }
        return closestNode;
    }

    private void ResetLostAgents()
    {
        for (int i = mainScript.agentList.Count - 1; i >= 0; i--)
        {
            Agent agent = mainScript.agentList[i];
            if(agent.isPreparingToBoard || agent.boarding)
            {
                /**
                agent.isPreparingToBoard = false;
                agent.boarding = false;
                agent.pathIndex = 1;
                agent.Reset();
                agent.transform.position = new Vector3(Mathf.Clamp(agent.transform.position.x, -7.5f, 7.5f), 0f, agent.transform.position.z);
                agent.isWaiting = true;
                waitingAreaController.waitingAgents.Add(agent);
                */
                mainScript.agentList.RemoveAt(i);
                Destroy(agent.gameObject);
            }
            if(agent.isAlighting)
            {
                agent.Reset();
            }
        }
    }
}
