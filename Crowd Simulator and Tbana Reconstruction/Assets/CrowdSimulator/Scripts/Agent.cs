using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Linq;


public class Agent : MonoBehaviour
{
	public Vector3 preferredVelocity, continuumVelocity, collisionAvoidanceVelocity;
	public Vector3 velocity;
	public List<int> path;
	internal int pathIndex = 0;
	internal float agentRelXPos, agentRelZPos;
	internal float neighbourXWeight, neighbourZWeight, neighbourXZWeight, selfWeight;
	internal float selfRightVelocityWeight, selfLeftVelocityWeight, selfUpperVelocityWeight, selfLowerVelocityWeight,
	neighbourRightVelocityWeight, neighbourLeftVelocityWeight, neighbourUpperVelocityWeight, neighbourLowerVelocityWeight;
	internal float densityAtAgentPosition;

	internal Vector3 targetPoint;
	internal bool done = false;
	internal bool noMap = false;
	internal Vector3 noMapGoal;
	internal int goal;
	internal Animator animator;
	internal Rigidbody rbody;
	internal bool collision = false;
	internal int row, column;
	Vector3 prevPos;
	Vector3 previousDirection;
	public float walkingSpeed;
	public float maxWaitTime = 2f;
	private bool isProblem = false;

	// Waiting
	internal bool isWaitingAgent;
	internal WaitingArea waitingArea;
	internal int waitingSpot;
	// Subway
	internal int trainLine;
	public bool isWaiting = false;
	public bool isPreparingToBoard = false;
	public bool boarding = false;
	public bool isAlighting = false;
	internal bool crossingYellowLine = false;
	private TrainController trainController;

	// Travel time
	internal float travelTime = 0f;
	internal float startTime;

	// Travel distance
	internal Vector3 previousPosition;
	internal float travelDistance = 0f;
	// Speed
	internal float movingTime = 0f;

	internal Transform tr;

	// Delay
	internal float delayTimer = 0f;
	internal bool isWaitingForDelay = false;

	internal void setDelay(float delay)
	{
		delayTimer = delay;
		isWaitingForDelay = true;
	}

	private Main mainScript;

	private float movementMeasureTimer = 0f;
	private float movementMeasureInterval = 0.1f;
	internal float travelDistanceTest = 0f;
	internal float movingTimeTest = 0f;
	internal Vector3 previousPositionTest;
	private float colliderRadius;
	internal Renderer agentRenderer;


	void Awake()
	{
		tr = transform;
	}

	internal void CheckPositionAndRotation()
	{
		Vector3 pos = tr.position;
    	Quaternion rot = tr.rotation;
    
		if (pos.y > 0.1f ||
			pos.y < -0.1f ||
			rot.x < -0.1 ||
			rot.x > 0.1 ||
			rot.z > 0.1 ||
			rot.z < -0.1)
			{
				//Debug.Log(tr.position.y + " " + tr.rotation.x + " " + tr.rotation.z);
				Reset();
				//Debug.DrawLine(agent.tr.position, agent.tr.position + Vector3.up * 5f, Color.red, 2f);
			}
	}

	internal void Start()
	{
		animator = tr.gameObject.GetComponent<Animator>();
		rbody = tr.gameObject.GetComponent<Rigidbody>();
		trainController = TrainController.instance;

		if (rbody != null)
		{
			rbody.isKinematic = false;
			rbody.useGravity = false;
		}
		else
		{
			Debug.LogError("No Rigidbody found!");
		}

		CapsuleCollider col = GetComponent<CapsuleCollider>();
		if (col == null)
		{
			Debug.LogError("No CapsuleCollider found!");
		}
		else
		{
			colliderRadius = col.radius;
		}

		//Which cell am i in currently?
		calculateRowAndColumn();
		if (!Grid.instance.colHandler && rbody != null)
		{
			Destroy(rbody);
		}

		mainScript = Main.instance;
		if (this is SubgroupAgent)
		{
			walkingSpeed = mainScript.agentMaxSpeed;
		}
		else
		{
			walkingSpeed = Random.Range(mainScript.agentMinSpeed, mainScript.agentMaxSpeed);
		}
		startTime = mainScript.simulationTime;

	}


	/* private void OnDrawGizmos()
	{
		UnityEditor.Handles.color = Color.red;
		if(isProblem)
		{
			UnityEditor.Handles.Label(tr.position + Vector3.up * 0.5f, "Problem!!!");
			Debug.DrawLine(tr.position, tr.position + Vector3.up * 5f, Color.red, 10f);
		}

		if(!noMap && pathIndex < path.Count)
		{
			UnityEditor.Handles.Label(tr.position + Vector3.up * 0.5f, path[pathIndex].ToString());
		}else if(pathIndex < path.Count)
		{
			//UnityEditor.Handles.Label(tr.position + Vector3.up * 0.5f, "noMap");
		}

		if(tr.position.y > 0.1f || 
			tr.position.y < -0.1f || 
			tr.rotation.x < -0.1 || 
			tr.rotation.x > 0.1 ||
			tr.rotation.z > 0.1 ||
			tr.rotation.z < -0.1)
		{
			Debug.Log("Problem: " + tr.position.y + " " + tr.rotation.x + " " + tr.rotation.z);
			Debug.DrawLine(tr.position, tr.position + Vector3.up * 5f, Color.red, 10f);
		}
		
	} */





	public void setWaitingAgent(bool isWaitingAgent)
	{
		this.isWaitingAgent = isWaitingAgent;
	}

	public void setNewPath(int start, int goal, MapGen.map map)
	{
		calculateRowAndColumn();
		this.goal = goal;

		path = map.shortestPaths[start][goal];

		pathIndex = 1;

		if (path.Count <= 1)
		{
			pathIndex = 0;
		}

		Vector3 pos = tr.position;

		targetPoint = map.allNodes[path[pathIndex]].getTargetPoint(pos, gameObject.GetInstanceID());

		//targetPoint = map.allNodes[path[pathIndex]].getTargetPoint(tr.position);
		preferredVelocity = (targetPoint - pos).normalized;

	}

	public void InitializeAgent(Vector3 pos, int start, int goal, MapGen.map map)
	{
		tr.position = pos;
		previousPosition = pos;
		previousPositionTest = pos;
		tr.right = tr.right;
		this.goal = goal;
		path = map.shortestPaths[start][goal];

		pathIndex = 1;
		targetPoint = map.allNodes[path[pathIndex]].getTargetPoint(pos, gameObject.GetInstanceID());
		preferredVelocity = (targetPoint - pos).normalized;
		agentRenderer = GetComponentInChildren<Renderer>();
		//tr.localScale = new Vector3(1.0f, 1.0f, 1.0f); // Modify this to change the size of characters new Vector3(2.0f, 2.0f, 2.0f) is normal size


	}

	public void ApplyMaterials(Material materialColor, Dictionary<string, int> skins, Material argMat = null)
	{
		if (tag == "original")
		{
			if (tr.childCount > 1)
			{
				//tr.GetChild(1).GetComponent<SkinnedMeshRenderer> ().sharedMaterial = materialColor;
			}
		}
		else if (tr.childCount > 0)
		{
			Renderer ss = tr.GetChild(0).GetComponent<Renderer>();
			if (ss != null)
				ss.material.mainTexture = (Texture)Resources.Load(tag + "-" + Random.Range(1, skins[tag] + 1));
			else
			{
				Renderer ss2 = tr.GetChild(1).GetComponent<Renderer>();
				if (ss2 != null)
					ss2.material.mainTexture = (Texture)Resources.Load(tag + "-" + Random.Range(1, skins[tag] + 1));
			}
		}
	}

	internal void calculateRowAndColumn()
	{
		Vector3 pos = tr.position;
		row = (int)((pos.z - Main.zMinMax.x) / Grid.instance.cellSize);
		column = (int)((pos.x - Main.xMinMax.x) / Grid.instance.cellSize);

		if (row < 0) row = 0;
		if (column < 0) column = 0;

		if (row > Grid.instance.nCellsZ - 1)
		{
			row = Grid.instance.nCellsZ - 1;
		}
		if (column > Grid.instance.nCellsX - 1)
		{
			column = Grid.instance.nCellsX - 1;
		}
		agentRelXPos = pos.x - Grid.instance.cellMatrix[row, column].transform.position.x;
		agentRelZPos = pos.z - Grid.instance.cellMatrix[row, column].transform.position.z;
	}

	/**
	 * Calculate the actual velocity of this agent, based on continuum, preferred and collision avoidance velocities
	 **/
	internal void setCorrectedVelocity()
	{
		calculateDensityAtPosition();
		calculateContinuumVelocity();
		//-1 since we subtract this agents density at position

		velocity = preferredVelocity + (densityAtAgentPosition - 1 / Mathf.Pow(Grid.instance.cellSize, 2)) / Grid.maxDensity
		* (continuumVelocity - preferredVelocity);
		velocity.y = 0f;
		if (velocity != Vector3.zero)
		{
			tr.forward = velocity.normalized;
		}
		velocity = velocity + collisionAvoidanceVelocity;
	}

	internal bool canSeeNext(MapGen.map map, int modifier)
	{
		if (pathIndex + modifier < path.Count && pathIndex + modifier >= 0 && pathIndex + modifier < map.allNodes.Count)
		{
			//Can we see next goal?
			Vector3 pos = tr.position;
			Vector3 next = map.allNodes[path[pathIndex + modifier]].getTargetPoint(pos, gameObject.GetInstanceID());
			int layersToIgnore = LayerMask.GetMask("WaitingAgent", "Agent");
			int layerMask = ~layersToIgnore;
			Vector3 targetPosition = pos - tr.forward * colliderRadius;
			Vector3 dir = next - targetPosition;
			Vector3 endPosition = targetPosition + (dir.normalized * dir.magnitude);
			if (!Physics.Raycast(targetPosition, dir.normalized, out RaycastHit hit, dir.magnitude, layerMask))
			{
				//Debug.DrawLine(targetPosition, endPosition, Color.green);
				return true;
			}
			else
			{
				//Debug.DrawLine(targetPosition, hit.point, Color.red);
			}
		}
		return false;
	}
	/**
	 * Calculate the preferred velocity by looking at desired path
	 **/
	internal void calculatePreferredVelocityMap(MapGen.map map)
	{
		bool change = false;
		previousDirection = preferredVelocity.normalized;
		Vector3 pos = tr.position;

		if (map.allNodes[path[pathIndex]].IsAgentInsideArea(pos) || (Grid.instance.skipNodeIfSeeNext && canSeeNext(map, 1)))
		{
			//New node reached
			collision = false;
			pathIndex += 1;
			if (pathIndex >= path.Count)
			{
				//Done
				done = true;
			}
			else
			{
				targetPoint = map.allNodes[path[pathIndex]].getTargetPoint(pos, gameObject.GetInstanceID());
				Vector3 nextDirection = (targetPoint - pos).normalized;
				if (Vector3.Angle(previousDirection, nextDirection) > 20.0f && Grid.instance.smoothTurns)
				{
					preferredVelocity = Vector3.RotateTowards(velocity.normalized, nextDirection, Grid.instance.dt * ((35.0f - 400 * Grid.instance.dt) * Mathf.PI / 180.0f), 15.0f).normalized;
					change = true;
				}
			}
		}
		else if (pathIndex > 0 && Grid.instance.walkBack && !canSeeNext(map, 0))
		{ //Can we see current heading? Are we trapped?
		  //No. We want to go back
			preferredVelocity = (map.allNodes[path[pathIndex - 1]].getTargetPoint(pos, gameObject.GetInstanceID()) - pos).normalized;
			change = false;
		}
		else
		{
			collision = false;
			Vector3 nextDirection = (targetPoint - pos).normalized;
			if (change && Vector3.Angle(previousDirection, nextDirection) > 20.0f && Grid.instance.smoothTurns)
			{
				preferredVelocity = Vector3.RotateTowards(velocity.normalized, nextDirection, Grid.instance.dt * ((35.0f - 400 * Grid.instance.dt) * Mathf.PI / 180.0f), 15.0f).normalized;
			}
			else
			{
				change = false;
				preferredVelocity = (targetPoint - pos).normalized;
			}
		}
		//collision = false;
		preferredVelocity = preferredVelocity * walkingSpeed;
		preferredVelocity.y = 0f;
	}

	/**
	 * Calculate the preferred velocity of a single uncharted point as a goal 
	 **/
	internal void calculatePreferredVelocityNoMap()
	{
		Vector3 pos = tr.position;
		if ((pos - noMapGoal).magnitude < MapGen.DEFAULT_THRESHOLD)
		{
			//New node reached
			//Done
			done = true;
		}
		else
		{
			preferredVelocity = (noMapGoal - pos).normalized;
		}
		preferredVelocity = preferredVelocity * walkingSpeed;
		preferredVelocity.y = 0f;
	}

	public void UpdateMetrics()
	{
		travelTime += Grid.instance.dt;

		Vector3 pos = tr.position;
		Vector3 delta = pos - previousPosition;
		float distance = delta.magnitude;
		if (distance > 0.001f)
		{
			travelDistance += distance;
			movingTime += Grid.instance.dt;
		}
		previousPosition = pos;

		movementMeasureTimer += Grid.instance.dt;

		if (movementMeasureTimer >= movementMeasureInterval)
		{
			Vector3 poss = tr.position;
			Vector3 deltaa = poss - previousPositionTest;
			float distancee = deltaa.magnitude;

			// Only count if movement is significant over the window
			if (distancee > 0.01f)
			{
				travelDistanceTest += distancee;
				movingTimeTest += movementMeasureTimer;
			}

			previousPositionTest = pos;
			movementMeasureTimer = 0f;
		}
	}

	internal virtual void calculatePreferredVelocity(MapGen.map map)
	{
		if (noMap)
		{
			calculatePreferredVelocityNoMap();
		}
		else
		{
			calculatePreferredVelocityMap(map);
		}
	}
	/**
	 * Change the position of the agent and reset variables. 
	 * Do animations.
	 **/
	internal void changePosition(MapGen.map map)
	{
		if (done)
		{
			return; // Don't do anything
		}

		calculatePreferredVelocity(map);
		if ((!trainController.dwelling[0] && !trainController.dwelling[1]) || isAlighting)
		{
			ApplyYellowLineForce();
		}
		setCorrectedVelocity();

		prevPos = tr.position;

		Vector3 newPosition = prevPos + velocity * Grid.instance.dt;
		newPosition.y = 0.0f;   // Lock Y position
		tr.position = newPosition;

		CheckYellowLine();

		if (rbody != null) { rbody.velocity = Vector3.zero; }
		collisionAvoidanceVelocity = Vector3.zero;

		Animate(prevPos);
	}

	internal void PassiveMove()
	{
		if (!trainController.dwelling[trainLine - 1])
		{
			ApplyYellowLineForce();
		}
		Vector3 force = collisionAvoidanceVelocity;
		force.y = 0f;

		if (force.magnitude > 0.01f)
		{
			Vector3 newPosition = tr.position + force * Grid.instance.dt;
			newPosition.y = 0f;
			tr.position = newPosition;
			tr.forward = force.normalized;

			CheckYellowLine();

			collisionAvoidanceVelocity = Vector3.zero;
			rotateAgent(trainController.mainScript.roadmap.allNodes[goal].transform.position);
		}

	}

	private void CheckYellowLine()
	{
		Vector3 pos = tr.position;
		if (trainController.dwelling[trainLine - 1] && !isAlighting) { return; }

		float positionX = Mathf.Abs(pos.x);

		switch (trainController.platformType)
		{
			case TrainController.PlatformType.Central:
				if (positionX > 8f && !crossingYellowLine)
				{
					if (trainController.mainScript.logger != null) trainController.mainScript.logger.LogYellowLineViolation(pos);
					Debug.DrawLine(pos, pos + Vector3.up * 10f, Color.red, 10f);
					crossingYellowLine = true;
				}
				if (crossingYellowLine && positionX < 8f)
				{
					crossingYellowLine = false;
				}
				break;

			case TrainController.PlatformType.Mixed:
				if (((positionX < 7f && positionX > 4f) ||
					 (positionX > 2f && positionX < 5f))
					 && !crossingYellowLine)
				{
					if (trainController.mainScript.logger != null) trainController.mainScript.logger.LogYellowLineViolation(pos);
					Debug.DrawLine(pos, pos + Vector3.up * 10f, Color.red, 10f);
					crossingYellowLine = true;
				}
				if (crossingYellowLine &&
				(positionX > 7f || positionX < 2f))
				{
					crossingYellowLine = false;
				}
				break;

			case TrainController.PlatformType.Side:
				if (positionX < 4f && !crossingYellowLine)
				{
					if (trainController.mainScript.logger != null) trainController.mainScript.logger.LogYellowLineViolation(pos);
					Debug.DrawLine(pos, pos + Vector3.up * 10f, Color.red, 10f);
					crossingYellowLine = true;
				}
				if (crossingYellowLine && positionX > 4f)
				{
					crossingYellowLine = false;
				}
				break;
		}
	}

	void Animate(Vector3 previousPosition)
	{
		float realSpeed = Vector3.Distance(tr.position, previousPosition) / Mathf.Max(Grid.instance.dt, Time.deltaTime);
		if (animator != null)
		{

			if (realSpeed < 0.05f)
			{
				animator.speed = 0;
			}
			else if (realSpeed > walkingSpeed)
			{
				animator.speed = 1;
			}
			else
			{
				animator.speed = realSpeed / walkingSpeed;
			}
		}
	}

	/**
	 * Do a bilinear interpolation of surrounding densities and come up with a density at this agents position.
	 **/
	internal float calculateDensityAtPosition()
	{
		densityAtAgentPosition = 0.0f;
		int xNeighbour = (int)(column + neighbourXWeight / Mathf.Abs(neighbourXWeight));    //Column for the neighbour which the agent contributes to
		int zNeighbour = (int)(row + neighbourZWeight / Mathf.Abs(neighbourZWeight));       //Row for the neighbour which the agent contributes to

		densityAtAgentPosition += Mathf.Abs(selfWeight) * Grid.instance.density[row, column];

		if (xNeighbour >= 0 && xNeighbour < Grid.instance.nCellsX)
		{   //As long as the cell exists
			densityAtAgentPosition += Mathf.Abs(neighbourXWeight) * Grid.instance.density[row, xNeighbour];
		}

		if (zNeighbour >= 0 && zNeighbour < Grid.instance.nCellsZ)
		{           //As long as the cell exists
			densityAtAgentPosition += Mathf.Abs(neighbourZWeight) * Grid.instance.density[zNeighbour, column];
		}

		if (zNeighbour >= 0 && zNeighbour < Grid.instance.nCellsZ && xNeighbour >= 0 && xNeighbour < Grid.instance.nCellsX)
		{   //As long as the cell exists
			densityAtAgentPosition += Mathf.Abs(neighbourXZWeight) * Grid.instance.density[zNeighbour, xNeighbour];
		}
		return densityAtAgentPosition;
	}

	/**
	 * Calculate the continuum velocity caused by pressure from the grid
	 **/
	internal void calculateContinuumVelocity()
	{
		Vector3 tempContinuumVelocity = Vector3.zero;

		int xNeighbour = (int)(column + neighbourXWeight / Mathf.Abs(neighbourXWeight));    //Column for the neighbour which the agent contributes to
		int zNeighbour = (int)(row + neighbourZWeight / Mathf.Abs(neighbourZWeight));       //Row for the neighbour which the agent contributes to

		// Sides in current cell
		tempContinuumVelocity.x += selfLeftVelocityWeight * Grid.instance.cellMatrix[row, column].leftVelocityNode.velocity;
		tempContinuumVelocity.x += selfRightVelocityWeight * Grid.instance.cellMatrix[row, column].rightVelocityNode.velocity;
		tempContinuumVelocity.z += selfUpperVelocityWeight * Grid.instance.cellMatrix[row, column].upperVelocityNode.velocity;
		tempContinuumVelocity.z += selfLowerVelocityWeight * Grid.instance.cellMatrix[row, column].lowerVelocityNode.velocity;

		if (zNeighbour >= 0 && zNeighbour < Grid.instance.nCellsZ)
		{   //As long as the cell exists
			tempContinuumVelocity.x += neighbourLeftVelocityWeight * Grid.instance.cellMatrix[zNeighbour, column].leftVelocityNode.velocity;
			tempContinuumVelocity.x += neighbourRightVelocityWeight * Grid.instance.cellMatrix[zNeighbour, column].rightVelocityNode.velocity;
		}

		if (xNeighbour >= 0 && xNeighbour < Grid.instance.nCellsX)
		{           //As long as the cell exists
			tempContinuumVelocity.z += neighbourUpperVelocityWeight * Grid.instance.cellMatrix[row, xNeighbour].upperVelocityNode.velocity;
			tempContinuumVelocity.z += neighbourLowerVelocityWeight * Grid.instance.cellMatrix[row, xNeighbour].lowerVelocityNode.velocity;
		}

		if (float.IsNaN(tempContinuumVelocity.x)) tempContinuumVelocity.x = 0;
		if (float.IsNaN(tempContinuumVelocity.z)) tempContinuumVelocity.z = 0;

		continuumVelocity = tempContinuumVelocity;
	}

	/**
	 * Move command (and all it includes) for this agent.
	 * Recalculate weights and contributions to grid after update.
	 **/
	internal void move(MapGen.map map)
	{
		changePosition(map);
		calculateRowAndColumn();
		setWeights();
		Grid.instance.cellMatrix[row, column].addVelocity(this);
		Grid.instance.cellMatrix[row, column].addDensity(this);
	}


	/**
	 * Set weight contributions to current cell radius. (Inverse bilinear interpolation)
	 **/
	public void setWeights()
	{
		float cellSize = Grid.instance.cellSize;
		float clSquared = Mathf.Pow(cellSize, 2);

		//An area the size of a cell is surrounded by each point.
		//AgentRelXPos: Side length of supposed area, outside current cell of agent - x direction
		//AgentRelZPos: Side length of supposed area, outside current cell of agent - z direction
		float sideOne = cellSize - Mathf.Abs(agentRelXPos); //Side length of supposed area of this agents position, x - direction
		float sideTwo = cellSize - Mathf.Abs(agentRelZPos); //Side length of supposed area of this agents position, z - direction

		// Weights on smaller areas inside and outside current cell
		//Area weight of neighboring cell in..
		neighbourXWeight = sideTwo * agentRelXPos / clSquared; // x direction
		neighbourZWeight = sideOne * agentRelZPos / clSquared; //z direction
		neighbourXZWeight = agentRelXPos * agentRelZPos / clSquared; //both x and z direction (diagonal from this agent's cell)
																	 //Own cell weight
		selfWeight = sideOne * sideTwo / clSquared;

		//Now checking velocityNodes contribution
		//Offsets from each velocity node's center (also seen as a cell on each node)
		float rightShiftedRelXPos = cellSize / 2 + agentRelXPos;
		float leftShiftedRelXPos = cellSize / 2 - agentRelXPos;
		float upperShiftedRelZPos = cellSize / 2 + agentRelZPos;
		float lowerShiftedRelZPos = cellSize / 2 - agentRelZPos;

		//Weight contributions to different velocityNodes (area / totalCellArea)
		selfRightVelocityWeight = rightShiftedRelXPos * sideTwo / clSquared;
		selfLeftVelocityWeight = leftShiftedRelXPos * sideTwo / clSquared;
		selfUpperVelocityWeight = upperShiftedRelZPos * sideOne / clSquared;
		selfLowerVelocityWeight = lowerShiftedRelZPos * sideOne / clSquared;

		neighbourRightVelocityWeight = rightShiftedRelXPos * Mathf.Abs(agentRelZPos) / clSquared;
		neighbourLeftVelocityWeight = leftShiftedRelXPos * Mathf.Abs(agentRelZPos) / clSquared;
		neighbourUpperVelocityWeight = upperShiftedRelZPos * Mathf.Abs(agentRelXPos) / clSquared;
		neighbourLowerVelocityWeight = lowerShiftedRelZPos * Mathf.Abs(agentRelXPos) / clSquared;
	}

	public void teleportAgent(Vector3 newPosition)
	{
		newPosition.y = 0.0f;
		tr.position = newPosition;
	}

	public void setAnimatorStanding(bool isStanding)
	{
		if (animator != null)
		{
			animator.SetBool("Standing", isStanding);
		}
	}

	public void rotateAgent(Vector3 target)
	{
		Vector3 direction = target - tr.position;
		tr.rotation = Quaternion.LookRotation(direction);
		rbody.velocity = Vector3.zero;
		rbody.angularVelocity = Vector3.zero;
	}

	internal void Reset()
	{
		rbody.velocity = Vector3.zero;
		rbody.angularVelocity = Vector3.zero;
		velocity = Vector3.zero;
		preferredVelocity = Vector3.zero;
		continuumVelocity = Vector3.zero;
		collisionAvoidanceVelocity = Vector3.zero;
		Vector3 pos = tr.position;
		tr.position = new Vector3(pos.x, 0f, pos.z);
		tr.rotation = Quaternion.identity;
	}

	private void ApplyYellowLineForce()
	{
		switch (trainController.platformType)
		{
			case TrainController.PlatformType.Central:
				ApplyYellowLineForceCentral();
				break;
			case TrainController.PlatformType.Mixed:
				ApplyYellowLineForceMixed();
				break;
			case TrainController.PlatformType.Side:
				ApplyYellowLineForceSide();
				break;
		}
	}

	private void ApplyYellowLineForceMixed()
	{
		float agentX = tr.position.x;

		// Side Platforms
		{
			float platformEdge = 6f;
			float yellowLineStart = 7.24f;
			float zoneWidth = yellowLineStart - platformEdge;

			// approaching from -12)
			if (agentX > -yellowLineStart && agentX < -platformEdge)
			{
				float distToEdge = -platformEdge - agentX;
				float strength = Mathf.Clamp01(distToEdge / zoneWidth);
				Vector3 repel = Vector3.left * strength * walkingSpeed;
				collisionAvoidanceVelocity += repel;
			}

			// approaching from +12)
			else if (agentX < yellowLineStart && agentX > platformEdge)
			{
				float distToEdge = agentX - platformEdge;
				float strength = Mathf.Clamp01(distToEdge / zoneWidth);
				Vector3 repel = Vector3.right * strength * walkingSpeed;
				collisionAvoidanceVelocity += repel;
			}
		}

		// Central Platform
		{
			float platformEdge = 3f;
			float yellowLineStart = 1.76f;
			float zoneWidth = platformEdge - yellowLineStart;

			if (agentX > -platformEdge && agentX < -yellowLineStart)
			{
				float distToEdge = agentX + platformEdge;
				float strength = Mathf.Clamp01(distToEdge / zoneWidth);
				Vector3 repel = Vector3.right * strength * walkingSpeed;
				collisionAvoidanceVelocity += repel;
			}

			else if (agentX < platformEdge && agentX > yellowLineStart)
			{
				float distToEdge = platformEdge - agentX;
				float strength = Mathf.Clamp01(distToEdge / zoneWidth);
				Vector3 repel = Vector3.left * strength * walkingSpeed;
				collisionAvoidanceVelocity += repel;
			}
		}
	}

	private void ApplyYellowLineForceCentral()
	{
		float agentX = tr.position.x;
		float platformEdge = 9f;
		float yellowLineStart = 7.76f;
		float zoneWidth = platformEdge - yellowLineStart;

		if (agentX > -platformEdge && agentX < -yellowLineStart)
		{
			float distToEdge = agentX + platformEdge;
			float strength = Mathf.Clamp01(distToEdge / zoneWidth);
			Vector3 repel = Vector3.right * strength * walkingSpeed;
			collisionAvoidanceVelocity += repel;
		}

		else if (agentX < platformEdge && agentX > yellowLineStart)
		{
			float distToEdge = platformEdge - agentX;
			float strength = Mathf.Clamp01(distToEdge / zoneWidth);
			Vector3 repel = Vector3.left * strength * walkingSpeed;
			collisionAvoidanceVelocity += repel;
		}
	}

	private void ApplyYellowLineForceSide()
	{
		float agentX = tr.position.x;

		float platformEdge = 3f;
		float yellowLineStart = 4.24f;
		float zoneWidth = yellowLineStart - platformEdge;

		// approaching from -
		if (agentX > -yellowLineStart && agentX < -platformEdge)
		{
			float distToEdge = -platformEdge - agentX;
			float strength = Mathf.Clamp01(distToEdge / zoneWidth);
			Vector3 repel = Vector3.left * strength * walkingSpeed;
			collisionAvoidanceVelocity += repel;
		}

		// approaching from +
		else if (agentX < yellowLineStart && agentX > platformEdge)
		{
			float distToEdge = agentX - platformEdge;
			float strength = Mathf.Clamp01(distToEdge / zoneWidth);
			Vector3 repel = Vector3.right * strength * walkingSpeed;
			collisionAvoidanceVelocity += repel;
		}
	}
}
