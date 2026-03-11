using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Distributions;

public class Grid : MonoBehaviour {

	public Cell cellPrefab;
	public VelocityNode velocityNodePrefab;

	internal static Grid instance; //Alive instance of this grid (should only be one)

	internal static float maxDensity; 
	internal float cellSize;
	internal float agentMaxSpeed;
	internal float agentAvoidanceRadius;
	internal float dt;
	internal float ringDiameter;
	internal float[] groupDistances;
	internal bool usePresetGroupDistances;
	
    // Decoupled dimensions
	internal int nCellsX;
    internal int nCellsZ;

	internal Main.LCPSolutioner solver;
	internal LCPSolver mprgpSolver; //LCP solver instance
	internal LCPSolverMIC mprgpmicSolver;
	internal LCPSolverProj psorSolver;
	internal int solverMaxIterations;
	internal double solverEpsilon;

	public Cell[,] cellMatrix;
	public VelocityNode[,] xEdgeVelocityNodeMatrix; //On vertical faces
	public VelocityNode[,] zEdgeVelocityNodeMatrix; //On horizontal faces
	internal float[,] density; //Per cell center 
	public float[,] xEdgeDensity;
	public float[,] zEdgeDensity;
	public float[,] xEdgeVelocity;
	public float[,] zEdgeVelocity;
	public float[,] xMeanVelocity;
	public float[,] zMeanVelocity;

	public double[,] matrixArray; //Helping array to create A of LCP
	public double[] xArray, lArray, bArray; //LCP arrays
	public List<List<LCPSolver.denseMatrixNode>> AArray; //Actual sparse matrix carrier of A
	internal List<List<List<int>>> neighMatrix; //Neighbourhood grid for pair-wise collision
	internal int neighbourBins; //Length of neighMatrix
	internal float lenOfBin; //Length of each "bin" in neighMatrix


	internal MapGen mapGen; //Reference to mapgen in main

	//Flags
	internal bool showSplattedDensity;
	internal bool showSplattedVelocity;
	internal bool walkBack;
	internal bool skipNodeIfSeeNext;
	internal bool smoothTurns;
	internal bool colHandler;
	internal bool[,] check;

	//Preset group distance values
	internal float pair = 0.54f;
	internal float trio = 0.585f;
	internal float quad = 0.656f;

	public void initGrid(Vector2 xMinMax, Vector2 zMinMax, float alpha, float agentAvoidanceRadius) {
		
        nCellsX = Mathf.CeilToInt((xMinMax.y - xMinMax.x) / cellSize);
        nCellsZ = Mathf.CeilToInt((zMinMax.y - zMinMax.x) / cellSize);
        int totalCells = nCellsX * nCellsZ;

		matrixArray = new double[totalCells, totalCells];
		maxDensity = 2f*alpha/(Mathf.Sqrt(3f)*Mathf.Pow(agentAvoidanceRadius, 2));
	
		float cellScale = showSplattedDensity ? cellSize : 0f;

		cellMatrix = new Cell[nCellsZ, nCellsX]; 
		xEdgeVelocityNodeMatrix = new VelocityNode[nCellsZ, nCellsX + 1];
		zEdgeVelocityNodeMatrix = new VelocityNode[nCellsZ + 1, nCellsX];
		
        density = new float[nCellsZ, nCellsX]; 
		xEdgeDensity = new float[nCellsZ, nCellsX + 1];
		zEdgeDensity = new float[nCellsZ + 1, nCellsX];
		xEdgeVelocity = new float[nCellsZ, nCellsX + 1];
		zEdgeVelocity = new float[nCellsZ + 1, nCellsX];
		xMeanVelocity = new float[nCellsZ, nCellsX];
		zMeanVelocity = new float[nCellsZ, nCellsX];

		AArray = new List<List<LCPSolver.denseMatrixNode>> ();
		for (int i = 0; i < totalCells; ++i) {
			AArray.Add (new List<LCPSolver.denseMatrixNode> ());
		}
		xArray = new double[totalCells];
		bArray = new double[totalCells];
		lArray = new double[totalCells];

		Vector3 startPos = new Vector3(xMinMax.x + 0.5f*cellSize, 0.01f, zMinMax.x + 0.5f*cellSize);
		for (int i = 0; i < nCellsZ; ++i) {
			for (int j = 0; j < nCellsX; ++j) {
				Cell cell = Instantiate (cellPrefab) as Cell;
				cell.transform.position = new Vector3 (startPos.x + j * cellSize, startPos.y, startPos.z + i * cellSize);
				cell.transform.parent = transform;
				cell.transform.localScale = new Vector3(cellScale, 0.01f, cellScale);
				cell.setProperties (i, j, cellSize);
				cell.setVelocityNodes();
				cell.calculateAvailableArea ();
				cellMatrix [i, j] = cell;
			}
		}

		//Create neighmatrix
		neighMatrix = new List<List<List<int>>> ();
		for (int i = 0; i < neighbourBins; ++i) {
			neighMatrix.Add (new List<List<int>>());
			for (int j = 0; j < neighbourBins; ++j) {
				neighMatrix [i].Add (new List<int> ());
			}
		}
		lenOfBin = Mathf.Max((xMinMax.y - xMinMax.x), (zMinMax.y - zMinMax.x)) / neighbourBins;

		mprgpSolver = new LCPSolver (); 
		mprgpmicSolver = new LCPSolverMIC();
		psorSolver = new LCPSolverProj ();
	}
		
	/**
	 * Make each cell accumelate the density from agents and convert them to a continous form
	 **/ 
	internal void updateCellDensity () {
		for (int i = 0; i < nCellsZ; ++i) {
			for (int j = 0; j < nCellsX; ++j) {
				cellMatrix [i, j].splatDensity ();
				if (showSplattedDensity) {
					cellMatrix [i, j].renderer.enabled = true;
					cellMatrix[i, j].setColor ();
				} else if(cellMatrix[i, j].renderer.enabled) {
					cellMatrix [i, j].renderer.enabled = false;
				}
			}
		}
	}

	/**
	 * Make each velocity contribute their density / velocity values to the continous form
	 **/ 
	internal void updateVelocityNodes() {
		for (int i = 0; i < nCellsZ; ++i) {
			for (int j = 0; j < nCellsX; ++j) {
				xEdgeVelocityNodeMatrix [i, j].updateValues ();
				zEdgeVelocityNodeMatrix [i, j].updateValues ();

				if(i == nCellsZ - 1)
					zEdgeVelocityNodeMatrix [i+1, j].updateValues ();
				if(j == nCellsX - 1)
					xEdgeVelocityNodeMatrix [i, j+1].updateValues ();
			}
		}
		if (showSplattedVelocity) {
			for (int i = 0; i < nCellsZ; ++i) {
				for (int j = 0; j < nCellsX; ++j) {
					cellMatrix[i, j].drawVelocityField ();
				}
			}
		}
	}


	/**
	 * Set the mean velocity for each cell
	 **/ 
	internal void setMeanVelocities() {
		for (int i = 0; i < nCellsZ; ++i) {
			for (int j = 0; j < nCellsX; ++j) {
				cellMatrix [i, j].setMeanVelocity ();
			}
		}
	}

	/**
	 * Solve the linear constraint problem, with an option "clamped" if b-values should be clamped to non-negatives
	 **/ 
	public void solveLCP(bool clamped) {
		//Refresh containers
		for (int i = 0; i < AArray.Count; ++i) {
			AArray [i].Clear ();
			matrixArray [i, i] = 0;
		}

		//Calculate A and B matrices
		for (int i = 0; i < nCellsZ; ++i) {
			for (int j = 0; j < nCellsX; ++j) {
				constructB (i, j, clamped);
				constructA (i, j);
			}
		}

		switch (solver) {
		case Main.LCPSolutioner.mprgp:
			xArray = mprgpSolver.LCPSolve (AArray, matrixArray, bArray, xArray, lArray);
			break;
		case Main.LCPSolutioner.mprgpmic0:
			xArray = mprgpmicSolver.LCPSolve (AArray, matrixArray, bArray, xArray, lArray);
			break;
		case Main.LCPSolutioner.psor:
			xArray = psorSolver.LCPSolve (AArray, matrixArray, bArray, xArray, lArray);
			break;

		default:
			Debug.LogError ("Error: Invalid solver selected");
			break;
		}
	}

	/**
	 * Construct the b-matrix index at n m with values optionally clamped to non-negative values
	 **/ 
	internal void constructB(int n, int m, bool clamped){
		double temp = (double)(cellMatrix [n, m].availableArea * maxDensity - density [n, m] 
				+ ((xEdgeDensity[n, m+1]*xEdgeVelocity[n,m+1]
				+ zEdgeDensity[n+1, m]*zEdgeVelocity[n+1, m]
				- xEdgeDensity[n, m]*xEdgeVelocity[n, m]
				- zEdgeDensity[n, m]*zEdgeVelocity[n, m])/cellSize)*dt);

		if (clamped && temp < 0) temp = 0; 
		bArray [n * nCellsX + m] = temp;
	}
		


	/**
	 * Construct the A-matrix index at i j. Do this for one puppet-matrix and one sparse matrix (same cost).
	 **/ 
	internal void constructA(int i, int j) {
		int startIndex = i * nCellsX + j;
		int currentRow = startIndex;
		double cls = Mathf.Pow (cellSize, 2);

		//Coeff for P_{i-1,j}
		if (i > 0) {
			matrixArray[currentRow, startIndex - nCellsX] = (double)-(dt*zEdgeDensity[i,j]/cls);
			LCPSolver.denseMatrixNode node = new LCPSolver.denseMatrixNode ();
			node.value = matrixArray [currentRow, startIndex - nCellsX]; node.colIndex = startIndex - nCellsX;
			AArray [currentRow].Add (node);
		} 

		//Coeff for P_{i,j-1}
		if (j > 0) {
			matrixArray[currentRow, startIndex - 1] = (double)-(dt*xEdgeDensity[i,j]/cls); 
			LCPSolver.denseMatrixNode node = new LCPSolver.denseMatrixNode ();
			node.value = matrixArray [currentRow, startIndex - 1]; node.colIndex = startIndex - 1;
			AArray [currentRow].Add (node);
		} 

		//Coeff for P_{i,j}
		matrixArray[currentRow, startIndex] = (double)(dt*(xEdgeDensity[i,j] + xEdgeDensity[i,j+1] + zEdgeDensity[i,j] + zEdgeDensity[i+1,j]))/cls;
		LCPSolver.denseMatrixNode nn = new LCPSolver.denseMatrixNode ();
		nn.value = matrixArray [currentRow, startIndex]; nn.colIndex = startIndex;
		AArray [currentRow].Add (nn);

		//Coeff for P_{i+1,j}
		if (i < nCellsZ - 1) {
			matrixArray[currentRow, startIndex + nCellsX] = (double)-(dt*zEdgeDensity[i+1,j]/cls);
			LCPSolver.denseMatrixNode node = new LCPSolver.denseMatrixNode ();
			node.value = matrixArray [currentRow, startIndex + nCellsX]; node.colIndex = startIndex + nCellsX;
			AArray [currentRow].Add (node);
		}

		//Coeff for P_{i,j+1}
		if (j < nCellsX - 1) {
			matrixArray[currentRow, startIndex + 1] = (double)-(dt*xEdgeDensity[i,j+1]/cls);
			LCPSolver.denseMatrixNode node = new LCPSolver.denseMatrixNode ();
			node.value = matrixArray [currentRow, startIndex + 1]; node.colIndex = startIndex + 1;
			AArray [currentRow].Add (node);
		}
	}

	/**
	 * Perform solution of LCP.
	 **/ 
	internal void PsolveRenormPsolve() {
		solveLCP (true);
		for (int n = 0; n < nCellsZ; n++) {
			for (int m = 0; m < nCellsX; m++) {
				zEdgeVelocityNodeMatrix[n,m].calculatePressureGradient();
				zEdgeVelocityNodeMatrix[n,m].pSolve();
				xEdgeVelocityNodeMatrix[n,m].calculatePressureGradient();
				xEdgeVelocityNodeMatrix[n,m].pSolve();
				if (n == nCellsZ - 1) {
					zEdgeVelocityNodeMatrix[n+1,m].calculatePressureGradient();
					zEdgeVelocityNodeMatrix[n+1,m].pSolve();
				}
				if (m == nCellsX - 1) {
					xEdgeVelocityNodeMatrix[n,m+1].calculatePressureGradient();
					xEdgeVelocityNodeMatrix[n,m+1].pSolve();
				}
			}
		}
			
		for (int n = 0; n < nCellsZ; n++) {
			for (int m = 0; m < nCellsX; m++) {
				zEdgeVelocityNodeMatrix [n, m].renorm ();
				xEdgeVelocityNodeMatrix[n,m].renorm ();
				if (n == nCellsZ - 1) zEdgeVelocityNodeMatrix[n+1,m].renorm ();
				if (m == nCellsX - 1) xEdgeVelocityNodeMatrix[n,m+1].renorm ();
			}
		}

		solveLCP (false); //Solve again with corrected, normalized velocities.

		for (int n = 0; n < nCellsZ; n++) {
			for (int m = 0; m < nCellsX; m++) {
				zEdgeVelocityNodeMatrix[n,m].calculatePressureGradient();
				zEdgeVelocityNodeMatrix[n,m].pSolve();
				xEdgeVelocityNodeMatrix[n,m].calculatePressureGradient();
				xEdgeVelocityNodeMatrix[n,m].pSolve();
				if (n == nCellsZ - 1) {
					zEdgeVelocityNodeMatrix[n+1,m].calculatePressureGradient();
					zEdgeVelocityNodeMatrix[n+1,m].pSolve();
				}
				if (m == nCellsX - 1) {
					xEdgeVelocityNodeMatrix[n,m+1].calculatePressureGradient();
					xEdgeVelocityNodeMatrix[n,m+1].pSolve();
				}
			}
		}
	}

	/**
	 * Handle pair-wise collision for a set of agents with given agent.
	 **/ 
	internal void handleCollision(int a, int row, int col, ref List<Agent> agentList) {
		if (row < 0 || col < 0 || row >= neighbourBins || col >= neighbourBins)
			return;
		for(int i = 0; i < neighMatrix[row][col].Count; ++i) {
			int oa = neighMatrix [row] [col] [i];
			if (a == oa) continue;

			if(agentList[a].isWaiting && agentList[oa].isWaiting)
			{
				float standingDistance = 0.4f;
				Vector3 distance = agentList [a].transform.position - agentList [oa].transform.position;
				if (distance.magnitude < standingDistance) 
				{
					agentList [a].collisionAvoidanceVelocity += distance.normalized * (standingDistance - distance.magnitude) * agentList[a].walkingSpeed;
				}
				continue;
			}
			if(agentList[a].isWaiting || (agentList[a].isPreparingToBoard && agentList[a].done))
				continue;

			float bumpDiameter = 0.4f;
			Vector3 dis = agentList [a].transform.position - agentList [oa].transform.position;
			if (dis.magnitude < ringDiameter) { //Assumption: ringDiameter > pxpy

				agentList [a].collisionAvoidanceVelocity += dis.normalized * (ringDiameter - dis.magnitude) * agentList[a].walkingSpeed; //Push away
			}
			if(agentList[oa].isWaiting && dis.magnitude <= bumpDiameter)
			{
				//agentList[oa].collisionAvoidanceVelocity -= dis.normalized * (bumpDiameter - dis.magnitude) * agentList[oa].walkingSpeed * 2f;
				Vector3 walkDir = agentList[a].preferredVelocity.normalized;
				Vector3 sideDir = Vector3.Cross(walkDir, Vector3.up);

				// Decide left or right based on relative position
				Vector3 relative = agentList[oa].transform.position - agentList[a].transform.position;
				float sideSign = Mathf.Sign(Vector3.Dot(relative, sideDir));
				Vector3 bumpDir = sideDir * sideSign;
				agentList[oa].collisionAvoidanceVelocity += bumpDir * agentList[a].walkingSpeed * 0.5f;
			}
		}
	}

	/**
	 * Do pair-wise collision avoidance for a set of agents, with respect to surrounding columns and rows.
	 **/ 
	internal void collisionHandling(ref List<Agent> agentList) {
	//	check = new bool[agentList.Count, agentList.Count];

		calculateNeighborList (ref agentList);
		for (int i = 0; i < agentList.Count; ++i) {
			int row = (int)((agentList [i].transform.position.z - Main.zMinMax.x) / lenOfBin); 
			int column = (int)((agentList[i].transform.position.x - Main.xMinMax.x) / lenOfBin); 
			row = Mathf.Clamp(row, 0, neighbourBins - 1);
			column = Mathf.Clamp(column, 0, neighbourBins - 1);

			handleCollision (i, row, column, ref agentList); 
			handleCollision (i, row+1, column, ref agentList); 
			handleCollision (i, row+1, column+1, ref agentList); 
			handleCollision (i, row, column+1, ref agentList); 
			handleCollision (i, row-1, column+1, ref agentList); 
			handleCollision (i, row-1, column, ref agentList); 
			handleCollision (i, row-1, column-1, ref agentList); 
			handleCollision (i, row, column-1, ref agentList); 
			handleCollision (i, row-1, column-1, ref agentList); 
		}
	}

	/**
	 * For each agent, calculate its position in a neighborhood bin.
	 **/ 
	internal void calculateNeighborList(ref List<Agent> agents) {
		for (int i = 0; i < neighMatrix.Count; ++i) {
			for (int j = 0; j < neighMatrix [i].Count; ++j) {
				neighMatrix [i] [j].Clear ();
			}
		}

		for (int i = 0; i < agents.Count; ++i) {
			int row = (int)((agents[i].transform.position.z - Main.zMinMax.x)/lenOfBin); 
			int column = (int)((agents[i].transform.position.x - Main.xMinMax.x)/lenOfBin); 
			row = Mathf.Clamp(row, 0, neighbourBins - 1);
			column = Mathf.Clamp(column, 0, neighbourBins - 1);
			neighMatrix [row] [column].Add (i);
		}
	}
}





///**
//	 * Handle pair-wise collision for a set of agents with given agent.
//	 **/ 
//internal void handleCollision(int a, int row, int col, ref List<Agent> agentList) {
//	if (row < 0 || col < 0 || row >= neighbourBins || col >= neighbourBins)
//		return;
//	for(int i = 0; i < neighMatrix[row][col].Count; ++i) {
//		int oa = neighMatrix [row] [col] [i];
//		if (a == oa )
//			continue;
//
//		Vector3 dis = agentList [a].transform.position - agentList [oa].transform.position;
//		if (dis.magnitude < ringDiameter) { //Assumption: ringDiameter > pxpy
//			if (agentList[a] is SubgroupAgent && agentList[oa] is SubgroupAgent && (agentList[a] as SubgroupAgent).c.tag.Equals ((agentList[oa] as SubgroupAgent).c.tag)) { //Allow modification of value later
//				if (useSpecificGroupDistances) {
//					int posA = ((SubgroupAgent)agentList [a]).number;
//					int posB = ((SubgroupAgent)agentList [oa]).number;
//					int count = ((SubgroupAgent)agentList [a]).c.comp.Count;
//					int disSlot = 0; 
//					bool pair = false;
//
//					if ((posA == 4 && posB == 2) || (posA == 2 && posB == 4) || (posA == 2 && posB == 0) || (posA == 0 && posB == 2) || (posA == 0 && posB == 1) || (posA == 1 && posB == 0) || (posA == 1 && posB == 3) || (posA == 3 && posB == 1)) {
//						pair = true;
//
//						switch (posA) {
//						case 4:
//							break;
//						case 2:
//							if (posB == 0 && ((SubgroupAgent)agentList [a]).c.assigned [4]) {
//								disSlot += 1;
//							} 
//							break;
//						case 0:
//							if (posB == 2 && ((SubgroupAgent)agentList [a]).c.assigned [4]) {
//								disSlot += 1;
//							} else if (posB == 1) {
//								if (((SubgroupAgent)agentList [a]).c.assigned [2]) {
//									disSlot += 1;
//									if (((SubgroupAgent)agentList [a]).c.assigned [4]) {
//										disSlot += 1;
//									}
//								}
//							}
//							break;
//						case 1:
//							if (posB == 0) {
//								if (((SubgroupAgent)agentList [a]).c.assigned [2]) {
//									disSlot += 1;
//									if (((SubgroupAgent)agentList [a]).c.assigned [4]) {
//										disSlot += 1;
//									}
//								}
//							} else if (posB == 3) {
//								if (((SubgroupAgent)agentList [a]).c.assigned [0]) {
//									disSlot += 1;
//									if (((SubgroupAgent)agentList [a]).c.assigned [2]) {
//										disSlot += 1;
//									}
//								}
//							}
//							break;
//						case 3:
//							if (posB == 1) {
//								if (((SubgroupAgent)agentList [a]).c.assigned [0]) {
//									disSlot += 1;
//									if (((SubgroupAgent)agentList [a]).c.assigned [2]) {
//										disSlot += 1;
//									}
//								}
//							}
//							break;
//
//						default:
//							break;
//						}
//					}
//					float[] distances = groupDistances;
//					if (usePresetValues) {
//						switch (((SubgroupAgent)agentList [a]).c.comp.Count) {
//						case 2:
//							distances = this.pair;
//							break;
//						case 3:
//							distances = this.trio;
//							break;
//						case 4:
//							distances = this.quad;
//							break;
//						default: 
//							break;
//						}
//					}
//					if (pair && dis.magnitude < 2 * distances [disSlot]) {
//
//						agentList [a].collisionAvoidanceVelocity += dis.normalized * (2 * distances [disSlot] - dis.magnitude) * agentMaxSpeed; 
//					}
//				} else {
//					agentList [a].collisionAvoidanceVelocity += dis.normalized * (2 * ringDiameter - dis.magnitude) * agentMaxSpeed; 
//				}
//			} else {
//				agentList [a].collisionAvoidanceVelocity += dis.normalized * (ringDiameter - dis.magnitude) * agentMaxSpeed; //Push away
//			}
//		}
//	}
//}
