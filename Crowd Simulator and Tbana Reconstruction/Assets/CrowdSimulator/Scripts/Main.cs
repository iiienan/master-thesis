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
	public int maxNumberOfAgents = 1000; // Maximum number of agents when spawning continuously

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
	internal int nEnteringAgents = 0;
	internal bool exitDone = false;
	internal bool enterDone = false;
	public ExperimentHUD experimentHUD;
	private float simulationStartTimer = 3f;
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

		if (testController.log) logger = FindObjectOfType<Logger>();
		if (logger == null && testController.log)
		{
			Debug.LogError("Logger not found in scene");
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

		nExitingAgents = trainController.trains[0].GetComponent<Train>().numberOfAgents + trainController.trains[1].GetComponent<Train>().numberOfAgents;

		if (customTimeStep)
		{
			Physics.simulationMode = SimulationMode.Script;
			Debug.Log("Simulation mode set to Script");
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
		// Update grid with new density and velocity values
		simulationGrid.updateCellDensity();
		simulationGrid.updateVelocityNodes();
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
		}

		//Pair-wise collision handling between agents
		simulationGrid.collisionHandling(agentList);

		trainController.TrainControllerUpdate();

		// Update spawners
		if(spawnAgents)
		{
			for (int i = 0; i < roadmap.spawns.Count; ++i)
			{
				if (agentList.Count < testController.entryFlow)
				{
					roadmap.spawns[i].spawner.UpdateSpawner();
				}
			}
		}
		
		// Log metrics
		if (testController.log)
		{
			TakeScreenshot();
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
		if (agent.isAlighting && agent.noMap)
		{
			agent.noMap = false;
			agent.done = false;
			MoveAgent(agent, true);
			return true;
		}
		// Agent reached the waiting area
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
			Debug.Log("Simulation started");
		}
	}

	private void EndSimulation()
	{
		if (nExitingAgents <= 0 && !exitDone)
		{
			if (testController.log) { logger.LogEvent("All exiting agents have exited the platform"); }
			Debug.Log("All exiting agents have exited the platform");
			exitDone = true;

		}
		if(nEnteringAgents <= 0 && !enterDone)
		{
			enterDone = true;
		}
		if(exitDone && enterDone && trainController.done)
		{
			if (testController.log) { logger.LogEvent("Simulation ended"); }
			Debug.Log("Simulation ended");
			UnityEditor.EditorApplication.isPlaying = false;
		}
	}

	private void TakeScreenshot()
	{
		if (losVisualizer != null && simulationTime >= testController.arriveInterval && takeScreenshot && agentList.Count >= testController.entryFlow)
		{
			losVisualizer.UpdateLOS();
			losVisualizer.takeScreenshot();
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
			Debug.Log("Agent outside of bounds, removing");
			agentList.RemoveAt(index);
			Destroy(agent.gameObject);
		}
	}

	private void HandleAgentDone(Agent agent, int index)
	{
		if (testController.log)
		{
			LogMetrics(agent);
		}

		if (agent.boarding)
		{
			trainController.nBoardingAgents[agent.trainLine - 1]--;
			nEnteringAgents--;
			agentList.RemoveAt(index);
			Destroy(agent.gameObject);
		}
		else if (agent.isAlighting)
		{
			nExitingAgents--;
			agentList.RemoveAt(index);
			Destroy(agent.gameObject);
		}
	}

	private void LogMetrics(Agent agent)
	{
		float travelTime = simulationTime - agent.startTime;

		float efficiency = agent.shortestPath / agent.travelDistance;
		if (efficiency > 1f)
		{
			Debug.LogWarning("Efficiency above 1: " + efficiency + " Difference: " + (agent.shortestPath - agent.travelDistance));
			efficiency = 1f;
			Debug.DrawLine(agent.tr.position, agent.tr.position + Vector3.up * 5f, Color.red, 10f);
		}

		float averageSpeed = agent.activeTravelDistance / agent.movingTime;

		if (agent.boarding)
		{
			logger.LogTravelTime(travelTime, true, agent.trainLine, agent.startTime);
			logger.LogTravelDistance(agent.travelDistance, true, agent.trainLine, averageSpeed, efficiency);

		}
		else if (agent.isAlighting)
		{
			logger.LogTravelTime(travelTime, false, agent.trainLine, agent.startTime);
			logger.LogTravelDistance(agent.travelDistance, false, agent.trainLine, averageSpeed, efficiency);

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
