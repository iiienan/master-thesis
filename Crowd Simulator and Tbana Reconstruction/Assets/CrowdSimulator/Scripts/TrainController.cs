using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class TrainController : MonoBehaviour
{
    public static TrainController instance;
    public bool spawnTrains = true;
    public GameObject[] trains = new GameObject[2];
    private float arrivalTimer = 0f;
    internal float arriveInterval = 10f;
    public enum TrainState { Incoming, Arrived, BoardingAlighting, Exiting}
    private TrainState[] trainStates = new TrainState[2];
    private float[] stateTimer = new float[2];

    private WaitingAreaController waitingAreaController;
    internal Main mainScript;
    internal int[] nBoardedAgents = new int[2];
    internal int nAgents = 100;
    public bool waitForMinimumAgents = false;
    internal bool[] isPreparingToBoard = new bool[2];
    internal bool[] boarding = new bool[2];
    internal bool alightBeforeBoarding = true;

    public enum PlatformType {Central, Mixed, Side}
    public PlatformType platformType;

    public enum Flow{Symmetric, Asymmetric}
    internal Flow flow;
    internal int[] nBoardingAgents = new int[2];
    //public bool useDwellTimer = true;
    private Logger logger;
    private TestController testController;
    private bool[] allSpawnersDone = new bool[2];
    internal Train[] trainScripts = new Train[2];
    internal bool[] dwelling = new bool[2];
    internal bool waitOutsideTrain = false;
    private Vector3[] nodePositions;
    [SerializeField] private float arrivalDelay = 10f;
    [SerializeField] private float boardingDelay = 1f;
    [SerializeField] private float exitingDelay = 5f;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        waitingAreaController = FindObjectOfType<WaitingAreaController>();
        if (waitingAreaController == null)        {
            Debug.LogError("WaitingAreaController not found in scene");
            return;
        }
        testController = FindObjectOfType<TestController>();
        if (testController == null)        {
            Debug.LogError("TestController not found in scene");
            return;
        }
        mainScript = FindObjectOfType<Main>();
        if (mainScript == null)        {
            Debug.LogError("Main script not found in the scene.");
            return;
        }

        if (testController.log) logger = FindObjectOfType<Logger>();
        if (logger == null && testController.log)
        {
            Debug.LogError("Logger not found");
        }

        Debug.Log("Test Case: " + platformType + " " + testController.scenario + " " + flow + " AB: " + alightBeforeBoarding.ToString());

        Debug.Log("Number of entering agents: " + nAgents);

        Debug.Log("Number of exiting agents: Train 1: " + trains[0].GetComponent<Train>().numberOfAgents + " Train 2: " + trains[1].GetComponent<Train>().numberOfAgents);

        for(int i = 0; i < trains.Length; i++)
        {
            if(trains[i] != null) trainScripts[i] = trains[i].GetComponent<Train>();
            trainStates[i] = TrainState.Incoming;
            ToggleTrain(i, false);
            dwelling[i] = false;
        }

        nodePositions = new Vector3[mainScript.roadmap.allNodes.Count];
        for(int i = 0; i < mainScript.roadmap.allNodes.Count; i++)
        {
            nodePositions[i] = mainScript.roadmap.allNodes[i].transform.position;
        }
    }

    public void TrainControllerUpdate()
    {
        if (!spawnTrains) return;

        if(trainStates[0] == TrainState.Incoming && trainStates[1] == TrainState.Incoming)
        {
            arrivalTimer += Grid.instance.dt;
            
            if(arrivalTimer >= arriveInterval)
            {
                if(waitForMinimumAgents && mainScript.agentList.Count < nAgents)
                {
                    return;
                }
                arrivalTimer = 0f;
                TriggerArrival(0);
                TriggerArrival(1);
            }
        }

        HandleTrain(0);
        HandleTrain(1);
        
    }

    private void TriggerArrival(int trainLine)
    {
        trainStates[trainLine] = TrainState.Arrived;
        ToggleTrain(trainLine, true);
        dwelling[trainLine] = true;
        stateTimer[trainLine] = arrivalDelay;
        PrepareBoarding(trainLine);
        if(logger != null) logger.LogEvent("Train " + trainLine + " arrived");
        mainScript.spawnAgents = false;
    }

    private void ToggleTrain(int trainLine, bool active)
    {
        for(int i = 0; i <= 2; i++)
        {
            trains[trainLine].transform.GetChild(i).gameObject.SetActive(active);
        }
    }

    private void HandleTrain(int trainLine)
    {
        switch (trainStates[trainLine])
        {
            case TrainState.Arrived:
                stateTimer[trainLine] -= Grid.instance.dt;
                if (stateTimer[trainLine] <= 0f)
                {
                    foreach (var spawner in trainScripts[trainLine].trainSpawners)
                    {
                        spawner.isSpawning = true;
                    }
                    if (logger != null) logger.LogEvent("Train " + trainLine + " started alighting");

                    trainStates[trainLine] = TrainState.BoardingAlighting;

                    if(!alightBeforeBoarding)
                    {
                        Board(trainLine);
                    }
                }
                break;

            case TrainState.BoardingAlighting:

                bool spawnersDone = true;
                foreach (var spawner in trainScripts[trainLine].trainSpawners)
                {
                    spawner.UpdateSpawner();
                    if (!spawner.done)
                    {
                        spawnersDone = false;
                    }
                }

                if(spawnersDone && !allSpawnersDone[trainLine])
                {
                    allSpawnersDone[trainLine] = true;
                    if (logger != null) logger.LogEvent("Train " + trainLine + " finished alighting");
                    Debug.Log("Train " + trainLine + " finished alighting");
                    if(alightBeforeBoarding)
                    {
                        stateTimer[trainLine] = boardingDelay;
                    }

                }

                if(alightBeforeBoarding && spawnersDone && !boarding[trainLine])
                {
                    stateTimer[trainLine] -= Grid.instance.dt;
                    if(stateTimer[trainLine] <= 0f)
                    {
                        isPreparingToBoard[trainLine] = false;
                        Board(trainLine);
                    }
                }

                bool boardingComplete = nBoardingAgents[trainLine] <= 0;

                if(spawnersDone && boardingComplete && boarding[trainLine])
                {
                    if (logger != null) logger.LogEvent("Train " + trainLine + " finished boarding");
                    Debug.Log("Train " + trainLine + " finished boarding");
                    boarding[trainLine] = false;
                    trainStates[trainLine] = TrainState.Exiting;
                    stateTimer[trainLine] = exitingDelay;
                }
                break;

            case TrainState.Exiting:
                stateTimer[trainLine] -= Grid.instance.dt;
                if(stateTimer[trainLine] <= 0f)
                {
                    ToggleTrain(trainLine,false);
                    dwelling[trainLine] = false;

                    if (mainScript.nExitingAgents <= 0 && trainStates[0] == TrainState.Exiting && trainStates[1] == TrainState.Exiting)
                    {
                        UnityEditor.EditorApplication.isPlaying = false;
                    }
                }
                break;
        }
    }

    public void PrepareBoarding(int trainLine)
    {
        isPreparingToBoard[trainLine] = true;
        PrepareWaitingAgents(trainLine);
        PrepareWalkingAgents(trainLine);
        nBoardedAgents[trainLine] = 0;
    }

    public void PrepareWaitingAgents(int trainLine)
    {
        for (int i = 0; i < waitingAreaController.waitingAgents.Count; i++)
        {
            Agent agent = waitingAreaController.waitingAgents[i];
            if (agent.trainLine == trainLine+1)
            {
                agent.agentRenderer.material = waitingAreaController.boardingAgentMaterial;

                agent.isWaitingAgent = false;
                agent.waitingArea.freeWaitingSpots.Add(agent.waitingSpot);

                Rigidbody rb = agent.rbody;
                rb.constraints = RigidbodyConstraints.None;

                if(waitOutsideTrain)
                {
                    WaitOutsideTrain(agent);
                }
                else
                {
                    agent.done = true;
                    agent.isPreparingToBoard = true;
                    agent.isWaiting = false;
                }
             

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
            if (agent.trainLine == trainLine+1 && !agent.boarding && !agent.isWaiting)
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
        agent.agentRenderer.material = waitingAreaController.boardingAgentMaterial;

        int closestTrainDoor = waitingAreaController.FindClosestTrainDoor(agent);
        int closestNode = FindClosestNode(agent.tr.position);
        agent.setNewPath(closestNode, closestTrainDoor, mainScript.roadmap);

        if(agent.isWaitingAgent)
        {
            agent.waitingArea.freeWaitingSpots.Add(agent.waitingSpot);
            agent.isWaitingAgent = false;
        }
        if(waitOutsideTrain)
        {
            WaitOutsideTrain(agent);
        }
        else
        {
            agent.done = true;
            agent.isPreparingToBoard = true;
            agent.isWaiting = false;
        }

    }

    internal void WaitOutsideTrain(Agent agent)
    {
        if (!agent) return;
        
        // Wait outside the train close to the door
        Vector3 targetPoint = mainScript.roadmap.allNodes[agent.path[agent.pathIndex]].transform.position;
        Vector3 waitPosition;
        if (agent.tr.position.z < targetPoint.z)
        {
            waitPosition = new Vector3(targetPoint.x, 0, targetPoint.z - Random.Range(1.5f, 2.5f));
        }
        else
        {
            waitPosition = new Vector3(targetPoint.x, 0, targetPoint.z + Random.Range(1.5f, 2.5f));
        }

        if (agent.tr.position.x < targetPoint.x)
        {
            waitPosition.x = agent.tr.position.x + Random.Range(0.5f, 1f);
        }
        else
        {
            waitPosition.x = agent.tr.position.x + Random.Range(-1f, -0.5f);
        }

        if (platformType == PlatformType.Central)
        {
            waitPosition.x = Mathf.Clamp(waitPosition.x, -8.5f, 8.5f);
        }
        else if (platformType == PlatformType.Mixed)
        {
            if (agent.tr.position.x < targetPoint.x)
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
            if (agent.tr.position.x < targetPoint.x)
            {
                waitPosition.x = Mathf.Clamp(waitPosition.x, -12f, -3.5f);
            }
            else
            {
                waitPosition.x = Mathf.Clamp(waitPosition.x, 3.5f, 12f);
            }
        }

        agent.noMapGoal = waitPosition;
        agent.noMap = true;

        // Reset these values to avoid unexpected movement
        agent.velocity = Vector3.zero;
        agent.preferredVelocity = Vector3.zero;
        agent.continuumVelocity = Vector3.zero;
        agent.collisionAvoidanceVelocity = Vector3.zero;
        
        // Wait for a bit
        agent.setDelay(Random.Range(0.1f, 1f));

        // Start moving towards the train door
        agent.done = false;
        agent.isWaiting = false;
        agent.isPreparingToBoard = true;
    }

    public void Board(int trainLine)
    {
        if(logger != null) logger.LogEvent("Train " + trainLine + " started boarding");
        boarding[trainLine] = true;
        for (int i = mainScript.agentList.Count - 1; i >= 0; i--)
        {
            Agent agent = mainScript.agentList[i];
            if (agent.trainLine == trainLine+1)
            {
                if (!agent.isPreparingToBoard)
                {
                    continue;
                }
                BoardAgent(agent);
                nBoardingAgents[agent.trainLine-1]++;
            }
        }
    }

    private void BoardAgent(Agent agent)
    {
        waitingAreaController.waitingAgents.Remove(agent);
        agent.Reset();
        agent.isWaitingForDelay = true;
        agent.delayTimer = Random.Range(0.1f, 1f);

        if (waitingAreaController.agentContainer != null)
        {
            agent.tr.SetParent(waitingAreaController.agentContainer.transform);
        }
        else
        {
            agent.tr.SetParent(null);
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
            Vector3 nodePos = nodePositions[j];
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
}
