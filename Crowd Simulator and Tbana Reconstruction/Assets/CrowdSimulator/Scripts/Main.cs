using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Data.Common;

public class Main : MonoBehaviour {

	public enum LCPSolutioner {
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



	public Grid gridPrefab;
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
	public ExperimentHUD experimentHUD;
	private float simulationStartTimer = 3f;
	private bool simulationStarted = false;

	/**
	 * Initialize simulation by taking the user's options into consideration and spawn agents.
	 * Then create the Staggered Grid along with all cells and velocity nodes.
	**/
	void OnEnable()
	{
		bool error = false;
		if (error)
			return;

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

		waitingAreaController = FindObjectOfType<WaitingAreaController>();
		if (waitingAreaController != null)
		{
			waitingAreaController.Initialize();
		}
		trainController = FindObjectOfType<TrainController>();
		if (trainController == null)
		{
			Debug.LogError("TrainController not found in scene");
		}
		testController = FindObjectOfType<TestController>();
		if (testController == null)
		{
			Debug.LogError("TestController not found in scene");
		}
		if(testController.log) logger = FindObjectOfType<Logger>();
		if (logger == null && testController.log)
		{
			Debug.LogError("Logger not found in scene");
		}

		Grid grid = Instantiate(gridPrefab) as Grid;
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
		Grid.instance = grid;
		Grid.instance.initGrid(xMinMax, zMinMax, alpha, agentAvoidanceRadius);

		for (int i = 0; i < roadmap.spawns.Count; ++i)
		{
			//roadmap.spawns[i].spawner.InitializeSpawner (ref agentPrefabs, ref groupAgentPrefabs, ref shirtColorPrefab, ref roadmap, 
			//								 ref agentList, xMinMax, zMinMax, agentAvoidanceRadius);
			roadmap.spawns[i].spawner.InitializeSpawner(ref roadmap,
											 ref agentList, xMinMax, zMinMax, agentAvoidanceRadius);
		}

		nExitingAgents = trainController.trains[0].GetComponent<Train>().numberOfAgents + trainController.trains[1].GetComponent<Train>().numberOfAgents;

		if(customTimeStep)
		{
			Physics.simulationMode = SimulationMode.Script;
			Debug.Log("Simulation mode set to Script");
		}

		experimentHUD = FindObjectOfType<ExperimentHUD>();

	}


    /**
	 * Main simulation loop which is called every frame
	**/
    void Update () {

		if(!simulationStarted)
		{
			simulationStartTimer -= Time.deltaTime;
			if(simulationStartTimer <= 0f)
			{
				simulationStarted = true;
				experimentHUD.realTimeStart = Time.realtimeSinceStartup;
				Debug.Log("Simulation started");
			}
			else
			{
				return;
			}
		}

		Grid.instance.dt = customTimeStep ? timeStep : Time.deltaTime;
		simulationTime += Grid.instance.dt;

		Grid.instance.solver = solver;
		Grid.instance.solverEpsilon = epsilon;
		Grid.instance.solverMaxIterations = solverMaxIterations;

		// Update grid with new density and velocity values
		Grid.instance.updateCellDensity ();
		Grid.instance.updateVelocityNodes ();
		//Solve linear constraint problem
		Grid.instance.PsolveRenormPsolve ();

		

		//Move agents
		for (int i = agentList.Count - 1; i >= 0; i--)
		{
			Agent agent = agentList[i];

			if(agent.isWaitingForDelay)
			{
				agent.delayTimer -= Grid.instance.dt;
				if(agent.delayTimer <= 0f)
				{
					agent.isWaitingForDelay = false;
					agent.Reset();
				}
				else
				{
					continue;
				}
			}

			if (agent.tr.position.y > 0.1f ||
			agent.tr.position.y < -0.1f ||
			agent.tr.rotation.x < -0.1 ||
			agent.tr.rotation.x > 0.1 ||
			agent.tr.rotation.z > 0.1 ||
			agent.tr.rotation.z < -0.1)
			{
				//Debug.Log(tr.position.y + " " + tr.rotation.x + " " + tr.rotation.z);
				agent.Reset();
				//Debug.DrawLine(agent.tr.position, agent.tr.position + Vector3.up * 5f, Color.red, 2f);
			}


			if (agent.isWaiting)
			{
				agent.PassiveMove();
				continue;
			}
			if (agent.done && agent.isPreparingToBoard)
			{
				continue;
			}
			if (agent.done && agent.isAlighting && agent.noMap)
			{
				agent.noMap = false;
				agent.done = false;
				continue;
			}

			// remove agent if it is outside the bounds of the plane
			if (Mathf.Abs(agent.tr.position.x) > planeSizeX * 5f || Mathf.Abs(agent.tr.position.z) > planeSizeZ * 5f || agent.tr.position.y > 0.5f)
			{
				if (agent.isWaitingAgent)
				{
					agent.waitingArea.isOccupied[agent.waitingSpot] = false;
					agent.waitingArea.freeWaitingSpots.Add(agent.waitingSpot);
					agent.isWaitingAgent = false;
				}
				Debug.Log("Agent outside of bounds, removing");
				agentList.RemoveAt(i);
				Destroy(agent.gameObject);
			}

			if (agent.done)
			{
				if (agent.isWaitingAgent)
				{
					// Agent reached the waiting area
					if (!agent.noMap)
					{
						waitingAreaController.walkAgentToWaitingSpot(agent);
						agent.move(ref roadmap);
					}
					// Agent reached the waiting spot
					else
					{
						waitingAreaController.putAgentInWaitingArea(agent);
					}
				}
				else
				{
					if (agent.boarding)
					{
						trainController.nBoardingAgents[agent.trainLine-1]--;
						if(logger != null) logger.LogTravelTime(agent.travelTime, true, agent.trainLine, agent.startTime);
						agentList.RemoveAt(i);
						float averageSpeed = agent.travelDistance / agent.movingTime;
						if(logger != null) logger.LogTravelDistance(agent.travelDistance, true, agent.trainLine, averageSpeed);

						Destroy(agent.gameObject);
					}
					else if (agent.isAlighting)
					{
						if(logger != null) logger.LogTravelTime(agent.travelTime, false, agent.trainLine, agent.startTime);
						nExitingAgents--;
						agentList.RemoveAt(i);
						float averageSpeed = agent.travelDistance / agent.movingTime;
						if(logger != null) logger.LogTravelDistance(agent.travelDistance, false, agent.trainLine, averageSpeed);

						Destroy(agent.gameObject);
						if (nExitingAgents <= 0)
						{
							// All exiting agents have exited the platform, end simulation
							if (logger != null) { logger.LogEvent("All exiting agents have exited the platform"); }
							Debug.Log("All exiting agents have exited the platform");
							if (trainController.nBoardingAgents[0] <= 0 && trainController.nBoardingAgents[1] <= 0)
							{
								UnityEditor.EditorApplication.isPlaying = false;
							}

						}
					}
					
				}
				continue;
			}
			agent.move(ref roadmap);
			agent.rbody.velocity = Vector3.zero;
			agent.rbody.angularVelocity = Vector3.zero;
			agent.UpdateMetrics();
		}
		//Pair-wise collision handling between agents
		Grid.instance.collisionHandling(ref agentList);

		for (int i = 0; i < roadmap.spawns.Count; ++i)
		{
			roadmap.spawns[i].spawner.UpdateSpawner();
		}

		trainController.TrainControllerUpdate();

		if(customTimeStep)
		{
			Physics.Simulate(Grid.instance.dt);
		}

		experimentHUD.RegisterSimTick();

		//flags
		Grid.instance.showSplattedDensity = showSplattedDensity;
		Grid.instance.showSplattedVelocity = showSplattedVelocity;
		Grid.instance.walkBack = walkBack;
		Grid.instance.skipNodeIfSeeNext = skipNodeIfSeeNext;
		Grid.instance.smoothTurns = smoothTurns;
	}
	public void AddToAgentList(Agent agent)
	{
		agentList.Add(agent);
	}

}
