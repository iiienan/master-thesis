using UnityEngine;

public class VelocityNode : MonoBehaviour {

	internal int cellRow, cellCol;
	internal Vector3 velocityVector, tempVelocity;
	internal float density;
	internal bool typeX; //true is x, false is z
	internal float pressureGradient;
	internal float weights;
	internal float velocity;

	/**
	 * Initialize this velocity node with row, col and type
	 **/ 
	public void init(int cellRow, int cellCol, bool typeX) {
		this.cellRow = cellRow;
		this.cellCol = cellCol;
		this.typeX = typeX;
		density = 0;
		pressureGradient = 0;
		weights = 0;
		velocity = 0;
	}

	/**
	 * Approximate the derivative of the pressure for this node as a central difference between the two closest cells.
	 **/ 
	internal void calculatePressureGradient(){
		int index = cellRow*SimulationGrid.instance.nCellsX+cellCol;
		if (typeX){
			if (cellCol == 0){
				pressureGradient = (float)(SimulationGrid.instance.xArray[index] -  0)/SimulationGrid.instance.cellSize; //Boundary condition
			}
			else if(cellCol == SimulationGrid.instance.nCellsX){
				pressureGradient = (float)(0 - SimulationGrid.instance.xArray[index-1])/SimulationGrid.instance.cellSize; //Boundary condition
			}
			else {
				pressureGradient = (float)(SimulationGrid.instance.xArray[index] - SimulationGrid.instance.xArray[index-1])/SimulationGrid.instance.cellSize;
			}
		} else {
			if (cellRow == 0){
				pressureGradient = (float)(SimulationGrid.instance.xArray[index] - 0)/SimulationGrid.instance.cellSize;
			}
			else if(cellRow == SimulationGrid.instance.nCellsZ){
				pressureGradient = (float)(0 - SimulationGrid.instance.xArray[index-SimulationGrid.instance.nCellsX])/SimulationGrid.instance.cellSize; //Boundary condition
			}
			else {
				pressureGradient = (float)(SimulationGrid.instance.xArray[index] - SimulationGrid.instance.xArray[index-SimulationGrid.instance.nCellsX])/SimulationGrid.instance.cellSize;
			}
		}
	}

	/**
	 * Re-normalize this velocity and update the velocity field of this node.
	 **/ 
	public void renorm() {
		velocityVector = velocityVector.normalized * SimulationGrid.instance.agentMaxSpeed;
		updateStoredValues (); //Save total values of current vel and dens in larger grid
	}

	/**
	 * Smooth velocity field with pressure gradient, allowing less velocity.
	 **/ 
	public void pSolve() {
		velocity = velocity - pressureGradient;
		if (typeX) {
			SimulationGrid.instance.xEdgeVelocity [cellRow, cellCol] = velocity;
		} else {
			SimulationGrid.instance.zEdgeVelocity [cellRow, cellCol] = velocity;
		}
	}
		
	/**
	 * Update the velocity and density contributions from this grid.
	 **/ 
	internal void updateStoredValues() {
		if (typeX) {
			velocity = velocityVector.x;
			SimulationGrid.instance.xEdgeVelocity [cellRow, cellCol] = velocityVector.x;
			SimulationGrid.instance.xEdgeDensity  [cellRow, cellCol] = density;
		} else {
			velocity = velocityVector.z;
			SimulationGrid.instance.zEdgeVelocity [cellRow, cellCol] = velocityVector.z;
			SimulationGrid.instance.zEdgeDensity  [cellRow, cellCol] = density;
		}
	}

	/**
	 * Splat the collected velocity and density to a field representation on this node.
	 **/ 
	internal void updateValues() {
		if (weights > 0) {
			velocityVector = tempVelocity / (SimulationGrid.instance.cellSize * SimulationGrid.instance.cellSize * weights); //Splat (Change) *Mathf.Pow(Grid.instance.cellLength, 2)
		} else {
			velocityVector = Vector3.zero;
			velocity = 0;
		}
		density = weights / Mathf.Pow(SimulationGrid.instance.cellSize, 2); //Splat
		updateStoredValues (); //Save total values of current vel and dens in larger grid
		tempVelocity = Vector3.zero;
		weights = 0;
	}
}
