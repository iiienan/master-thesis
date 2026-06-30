using UnityEngine;
using System.Collections.Generic;

public class Main : MonoBehaviour
{
	public static Main instance;

	public enum LCPSolutioner
	{
		mprgp,
		mprgpmic0,
		psor
	}
	public float epsilon;
	public int solverMaxIterations;
	public LCPSolutioner solver;


	public float planeSizeX;
	public float planeSizeZ;



	public float agentAvoidanceRadius;
	public float agentMaxSpeed;
	public float agentMinSpeed;
	public bool usePresetGroupDistances;
	public float p1p2, p2p3, p3p4;



	public SimulationGrid gridPrefab;
	public NewSpawner spawnerPrefab;
	public MapGen mapGen;
	public Plane plane;
	internal static Vector2 xMinMax;
	internal static Vector2 zMinMax;
	internal MapGen.map roadmap;

	public int cellSize;
	public int neighbourBins;
	public int roadNodeAmount; // Number of nodes that are placed automatically
	public bool visibleMap; // Show or hide the nodes in the world
	internal float ringDiameter;

	public bool customTimeStep;
	public float timeStep;

	[Range(0.01f, 1f)]
	public float alpha;

	internal List<Agent> agentList = new List<Agent>();

	public bool showSplattedDensity = false;
	public bool showSplattedVelocity = false;
	public bool walkBack = false;
	public bool skipNodeIfSeeNext = false;
	public bool smoothTurns = false;
	public bool handleCollision = false;
	internal WaitingAreaController waitingAreaController;
	internal TrainController trainController;
	internal Logger logger;
	internal TestController testController;
	internal float simulationTime = 0f;
	internal int nExitingAgents = 0;
	internal int[] nEnteringAgents = new int[2];
	internal int[] nExitingAgentsPerLine = new int[2];
	internal bool exitDone = false;
	internal bool enterDone = false;
	public ExperimentHUD experimentHUD;
	private float simulationStartTimer = 1f;
	private bool simulationStarted = false;
	internal LOSVisualizer losVisualizer;
	private bool takeScreenshot = true;
	internal DensityLog densityLog;
	internal bool spawnAgents = true;
	private SimulationGrid simulationGrid;

	void Awake()
	{
		instance = this;
	}

	/**
	 * Initialize simulation by taking the user's options into consideration and spawn agents.
	 * Then create the Staggered Grid along with all cells and velocity nodes.
	**/
	void OnEnable()
	{
		plane.transform.localScale = new Vector3(planeSizeX, 1.0f, planeSizeZ);
		Vector3 planeLength = plane.getLengths(); //Staggered grid length
		xMinMax = new Vector2(plane.transform.position.x - planeLength.x / 2,
							   plane.transform.position.x + planeLength.x / 2);
		zMinMax = new Vector2(plane.transform.position.z - planeLength.z / 2,
							  plane.transform.position.z + planeLength.z / 2);

		ringDiameter = agentAvoidanceRadius * 2; //Prefered distance between two agents

		//Creates roadmap / pathfinding for agents based on map
		MapGen m = Instantiate(mapGen) as MapGen;
		roadmap = m.generateRoadMap(roadNodeAmount, xMinMax, zMinMax, visibleMap);

		testController = FindObjectOfType<TestController>();
		if (testController == null)
		{
			Debug.LogError("TestController not found in scene");
			return;
		}
		if (testController.log) logger = FindObjectOfType<Logger>();
		if (logger == null && testController.log)
		{
			Debug.LogError("Logger not found in scene");
			return;
		}
		waitingAreaController = FindObjectOfType<WaitingAreaController>();
		if (waitingAreaController != null)
		{
			waitingAreaController.Initialize();
		}
		else
		{
			Debug.LogError("WaitingAreaController not found in scene");
			return;
		}
		trainController = FindObjectOfType<TrainController>();
		if (trainController == null)
		{
			Debug.LogError("TrainController not found in scene");
			return;
		}
		losVisualizer = FindObjectOfType<LOSVisualizer>();
		if (losVisualizer == null)
		{
			Debug.LogError("LOSVisualizer not found in scene");
			return;
		}
		densityLog = FindObjectOfType<DensityLog>();
		if (densityLog == null)
		{
			Debug.LogError("DensityLog not found in scene");
			return;
		}

		SimulationGrid grid = Instantiate(gridPrefab) as SimulationGrid;
		grid.showSplattedDensity = showSplattedDensity;
		grid.showSplattedVelocity = showSplattedVelocity;
		grid.cellSize = cellSize;
		grid.agentMaxSpeed = agentMaxSpeed;
		grid.ringDiameter = ringDiameter;
		grid.usePresetGroupDistances = usePresetGroupDistances;
		grid.groupDistances = new float[] { p1p2, p2p3, p3p4 };
		grid.mapGen = mapGen;
		grid.dt = timeStep;
		grid.neighbourBins = neighbourBins;
		grid.solver = solver;
		grid.solverEpsilon = epsilon;
		grid.solverMaxIterations = solverMaxIterations;
		grid.colHandler = handleCollision;
		grid.agentAvoidanceRadius = agentAvoidanceRadius;
		SimulationGrid.instance = grid;
		SimulationGrid.instance.initGrid(xMinMax, zMinMax, alpha, agentAvoidanceRadius);

		for (int i = 0; i < roadmap.spawns.Count; ++i)
		{
			roadmap.spawns[i].spawner.InitializeSpawner(roadmap,xMinMax, zMinMax, agentAvoidanceRadius);
		}

		nExitingAgentsPerLine[0] = trainController.trains[0].GetComponent<Train>().numberOfAgents;
		nExitingAgentsPerLine[1] = trainController.trains[1].GetComponent<Train>().numberOfAgents;
		nExitingAgents = nExitingAgentsPerLine[0] + nExitingAgentsPerLine[1];
		

		if (customTimeStep)
		{
			Physics.simulationMode = SimulationMode.Script;
		}

		experimentHUD = FindObjectOfType<ExperimentHUD>();
		if (experimentHUD == null)
		{
			Debug.LogError("ExperimentHUD not found in scene");
			return;
		}

		simulationGrid = SimulationGrid.instance;

		//flags
		simulationGrid.showSplattedDensity = showSplattedDensity;
		simulationGrid.showSplattedVelocity = showSplattedVelocity;
		simulationGrid.walkBack = walkBack;
		simulationGrid.skipNodeIfSeeNext = skipNodeIfSeeNext;
		simulationGrid.smoothTurns = smoothTurns;
		simulationGrid.solver = solver;
		simulationGrid.solverEpsilon = epsilon;
		simulationGrid.solverMaxIterations = solverMaxIterations;
	}


	/**
	 * Main simulation loop which is called every frame
	**/
	void Update()
	{
		// Wait a few seconds before starting the simulation
		if(!simulationStarted)
		{
			StartSimulation();
			return;
		}

		simulationGrid.dt = customTimeStep ? timeStep : Time.deltaTime;
		GridParallelBridge.Instance.BatchAndRunFrameRaycasts(agentList, roadmap, simulationGrid);
		// Update grid with new density and velocity values
		simulationGrid.updateCellDensity();
		simulationGrid.updateVelocityNodes();

		GridParallelBridge.Instance.CopyManagedGridToNative(simulationGrid.density, simulationGrid.nCellsX, simulationGrid.nCellsZ);
		GridParallelBridge.Instance.CalculateAgentDensitiesInParallel(agentList, simulationGrid);

		//Solve linear constraint problem
		simulationGrid.PsolveRenormPsolve();


		//Move agents
		for (int i = agentList.Count - 1; i >= 0; i--)
		{
			Agent agent = agentList[i];

			agent.CheckPositionAndRotation();
			CheckOutsideBounds(agent, i);

			if (agent.isWaitingForDelay)
			{
				HandleAgentWaitingForDelay(agent);
				continue;
			}

			if (agent.done)
			{
				if (HandleSpecialCaseAgent(agent))
				{
					continue;
				}

				HandleAgentDone(agent, i);
				continue;
			}

			MoveAgent(agent, true);
			trainController.CheckAlightingAgent(agent);
		}

		//Pair-wise collision handling between agents
		//simulationGrid.collisionHandling(agentList);

		GridParallelBridge.Instance.RunParallelCollisionAvoidance(agentList, simulationGrid, xMinMax, zMinMax);

		trainController.TrainControllerUpdate();

		// Update spawners
		if(spawnAgents)
		{
			for (int i = 0; i < roadmap.spawns.Count; ++i)
			{
				int trainLine = roadmap.spawns[i].spawner.trainLine;
				if(nEnteringAgents[trainLine - 1] < testController.entryFlowLines[trainLine-1])
				{
					roadmap.spawns[i].spawner.UpdateSpawner();
				}
			}
		}
		
		// Log metrics
		if (testController.log)
		{
			//TakeScreenshot();
			densityLog.UpdateDensityLog();
		}

		if (customTimeStep)
		{
			Physics.Simulate(simulationGrid.dt);
		}

		experimentHUD.RegisterSimTick();

		// Check for simulation end condition
		EndSimulation();

		simulationTime += simulationGrid.dt;
	}

	private bool HandleSpecialCaseAgent(Agent agent)
	{
		if (agent.isWaiting)
		{
			MoveAgent(agent, false);	
			return true;
		}
		if (agent.isPreparingToBoard)
		{
			MoveAgent(agent, false);
			return true;
		}
		if (agent.agentType == TrainController.AgentType.Alighting && agent.noMap)
		{
			agent.noMap = false;
			agent.done = false;
			MoveAgent(agent, true);
			return true;
		}
		// Agent can see the waiting area
		if (agent.isWaitingAgent && !agent.noMap)
		{
			waitingAreaController.walkAgentToWaitingSpot(agent);
			MoveAgent(agent, true);
			return true;
		}
		// Agent reached the waiting spot
		if(agent.isWaitingAgent && agent.noMap)
		{
			waitingAreaController.SetWaitingAgent(agent);
			MoveAgent(agent, false);
			return true;
		}
		return false;
	}

	private void HandleAgentWaitingForDelay(Agent agent)
	{
		agent.delayTimer -= simulationGrid.dt;
		if (agent.delayTimer <= 0f)
		{
			agent.isWaitingForDelay = false;
			agent.Reset();
		}
				
	}

	private void StartSimulation()
	{
		simulationStartTimer -= Time.deltaTime;
		if (simulationStartTimer <= 0f)
		{
			simulationStarted = true;
			experimentHUD.realTimeStart = Time.realtimeSinceStartup;
		}
	}

	private void EndSimulation()
	{
		if (nExitingAgents <= 0 && !exitDone)
		{
			exitDone = true;

		}
		if(nEnteringAgents[0] <= 0 && nEnteringAgents[1] <= 0 && !enterDone)
		{
			enterDone = true;
		}
		if(exitDone && enterDone && trainController.done)
		{
			if(RunManager.Instance)
			{
				RunManager.Instance?.OnRunComplete();
			}
			else
			{
				if(testController.log)
				{
					logger.LogRunSummary();
					logger.CloseAllWriters();
				}
				UnityEditor.EditorApplication.isPlaying = false;
			}
		}
	}

	private void TakeScreenshot()
	{
		if (losVisualizer != null && simulationTime >= testController.arriveInterval && takeScreenshot && agentList.Count >= testController.entryFlow)
		{
			losVisualizer.UpdateLOS();
			losVisualizer.TakeScreenshot();
			takeScreenshot = false;
		}
	}

	private void CheckOutsideBounds(Agent agent, int index)
	{
		if (Mathf.Abs(agent.tr.position.x) > planeSizeX * 5f || Mathf.Abs(agent.tr.position.z) > planeSizeZ * 5f || agent.tr.position.y > 0.5f)
		{
			if (agent.isWaitingAgent)
			{
				agent.waitingArea.freeWaitingSpots.Add(agent.waitingSpot);
				agent.isWaitingAgent = false;
			}
			if(agent.agentType == TrainController.AgentType.Boarding && trainController.trainStates[agent.trainLine-1] == TrainController.TrainState.BoardingAlighting)
			{
				trainController.nAgentsToBoard[agent.trainLine - 1]--;
				nEnteringAgents[agent.trainLine - 1]--;
			}
			else if (agent.agentType == TrainController.AgentType.Alighting)
			{
				nExitingAgents--;
				nExitingAgentsPerLine[agent.trainLine - 1]--;
				if(nExitingAgentsPerLine[agent.trainLine - 1] <= 0)
				{
					logger.allAlightersExitedTimestamp[agent.trainLine - 1] = simulationTime;
				}
				if(!agent.exitedTrain)
				{
					trainController.nAgentsToAlight[agent.trainLine - 1]--;
				}
			}
			agentList.RemoveAt(index);
			Destroy(agent.gameObject);
			logger.LogWarning("Agent removed outside of bounds");
		}
	}

	private void HandleAgentDone(Agent agent, int index)
	{
		if (testController.log)
		{
			LogMetrics(agent);
		}

		if (agent.agentType == TrainController.AgentType.Boarding)
		{
			trainController.nAgentsToBoard[agent.trainLine - 1]--;
			nEnteringAgents[agent.trainLine - 1]--;
		}
		else if (agent.agentType == TrainController.AgentType.Alighting)
		{
			nExitingAgents--;
			nExitingAgentsPerLine[agent.trainLine - 1]--;
			if(nExitingAgentsPerLine[agent.trainLine - 1] <= 0 && testController.log)
			{
				logger.allAlightersExitedTimestamp[agent.trainLine - 1] = simulationTime;
			}
		}
		agentList.RemoveAt(index);
		Destroy(agent.gameObject);
	}

	private void LogMetrics(Agent agent)
	{
		float travelTime = simulationTime - agent.startTime;

		float efficiency = agent.shortestPath / agent.travelDistance;
		if (efficiency > 1f)
		{
			logger.LogWarning("Efficiency above 1: " + efficiency + " Difference: " + (agent.shortestPath - agent.travelDistance));
			efficiency = 1f;
		}

		float averageSpeed = agent.activeTravelDistance / agent.movingTime;

		if (agent.agentType == TrainController.AgentType.Boarding)
		{
			logger.totalTravelTime[0, agent.trainLine - 1] += travelTime;
			logger.totalDistance[0, agent.trainLine - 1] += agent.travelDistance;
			logger.totalSpeed[0, agent.trainLine - 1] += averageSpeed;
			logger.totalPathEfficiency[0, agent.trainLine - 1] += efficiency;
			logger.totalEntityDensity[0, agent.trainLine - 1] += agent.GetAverageEntityDensity();
			logger.totalSocialProximity[0, agent.trainLine - 1] += agent.GetAverageSocialProximity();
			logger.totalAgents[0, agent.trainLine - 1]++;
		}
		else if (agent.agentType == TrainController.AgentType.Alighting)
		{
			logger.totalTravelTime[1, agent.trainLine - 1] += travelTime;
			logger.totalDistance[1, agent.trainLine - 1] += agent.travelDistance;
			logger.totalSpeed[1, agent.trainLine - 1] += averageSpeed;
			logger.totalPathEfficiency[1, agent.trainLine - 1] += efficiency;
			logger.totalEntityDensity[1, agent.trainLine - 1] += agent.GetAverageEntityDensity();
			logger.totalSocialProximity[1, agent.trainLine - 1] += agent.GetAverageSocialProximity();
			logger.totalAgents[1, agent.trainLine - 1]++;
		}
	}

	private void MoveAgent(Agent agent, bool isMoving)
	{
		if(isMoving)
		{
			agent.move(roadmap);
		}
		else
		{
			agent.PassiveMove();
		}
		agent.TickMetrics(isMoving);
		agent.rbody.velocity = Vector3.zero;
		agent.rbody.angularVelocity = Vector3.zero;
	}

}
