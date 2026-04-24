using System.Diagnostics;
using System.Collections.Generic;
using System;

public class LCPSolver {


	public struct denseMatrixNode{
		public int colIndex;
		public double value;
		public denseMatrixNode(int c, double v) {
			colIndex = c; value = v;
		}
	}
	protected double alphaBar, gammaConstant, epsilon;
	protected double[] b, x, l;
	protected List<List<denseMatrixNode>> A;
	internal double smallEpsilon = 0.000000000000001;

	internal bool checkEndCondition() {

		double[] z = PlusMinusVec (TwoMulOne (x), b, true);
		//z >= 0
		bool conditionA = true;
		//x >= 0
		bool conditionB = true;
		//zTx = 0
		bool conditionC = false;
		double sum = 0.0;

		for (int i = 0; i < z.GetLength (0); ++i) {
			if (z [i] < 0)
				conditionA = false;
			if (x [i] < 0)
				conditionB = false;
			sum += z [i] * x [i];
		}
		if (Math.Abs (sum) < epsilon) {
			conditionC = true;
		}
		return conditionA && conditionB && conditionC;
	}


	public virtual double[] LCPSolve(List<List<denseMatrixNode>> aList, double[,] aMatrix, double[] bArray, double[] xArray, double[] lArray) {
		this.A = aList; this.b = bArray; this.x = xArray; this.l = lArray;

		gammaConstant = 1; //Around 1
		alphaBar = 1 / (2*frobeniusNormM() + smallEpsilon);
		epsilon = SimulationGrid.instance.solverEpsilon * frobeniusNormV (b); //Very very small..
		if (epsilon > 0.01)
			epsilon = 0.001;
			//		UnityEngine.Debug.Log ("Epsilon: " + epsilon);
		double[] r = PlusMinusVec(TwoMulOne(x), b, true); //Ax+b (Dostal says Ax-b)
		double[] p = phi(x);
		double alphaCG;
		int cnt = 0;
		int lim = SimulationGrid.instance.solverMaxIterations;
		Stopwatch s = new Stopwatch ();
		s.Start ();
		while (frobeniusNormV (v(x)) > epsilon && cnt < lim && !checkEndCondition()  ) {
			cnt += 1;

		double normB = frobeniusNormV (B(x));
		if ((normB * normB) <= gammaConstant * DotProduct (phiTilde(x), phi(x))) {
				//1. Trial Conjugate Gradient Step
				double[] Ap = TwoMulOne (p);
				alphaCG = DotProduct (r, p) / (DotProduct (p, Ap)  + smallEpsilon);
				double[] y = PlusMinusVec (x, scalarMult (alphaCG, p), false);
				double alphaF = calcAlphaF (p);
				if (alphaCG <= alphaF) {
					//2. Conjugate Gradient Step
						for (int i = 0; i < y.GetLength (0); ++i)
								x [i] = y [i];
	//				x = y;
					r = PlusMinusVec (r, scalarMult (alphaCG, Ap), false);
					double gamma = DotProduct (phi(y), Ap) / (DotProduct (p, Ap)  + smallEpsilon);
					p = PlusMinusVec (phi(y), scalarMult (gamma, p), false);
				} else {
						//3. Expansion Step
						x = PlusMinusVec (x, scalarMult(alphaF, p), false);
						r = PlusMinusVec (r, scalarMult (alphaF, Ap), false); //Why do this..?
						x = projection (PlusMinusVec (x, scalarMult (alphaBar, phi(x)), false));
						r = PlusMinusVec (TwoMulOne(x), b, true); //Dostal says Ax-b
						p = phi(x);
				}
			} else {
					//4. Proportioning Step
					double[] d = B(x);
					double[] Ad = TwoMulOne (d);
					alphaCG = DotProduct (r, d) / (DotProduct (d, Ad)+ smallEpsilon);
					x = PlusMinusVec (x, scalarMult(alphaCG, d), false);
					r = PlusMinusVec (r, scalarMult(alphaCG, Ad), false);
					p = phi(x);
			}
		}	
		//UnityEngine.Debug.Log ("Took : " + s.ElapsedMilliseconds + " ms" );
		if (cnt == lim)
			UnityEngine.Debug.Log ("Yep..");
	//	UnityEngine.Debug.Log ("Iterations: " + cnt);
		return x;
		}

	protected double[] g(double[] vec) {
		return PlusMinusVec(TwoMulOne (vec), b, true); //Ax + b (dostal has - instead)
	}

	protected double[] phi(double[] vec) {
		double[] tempPhi = new double[vec.GetLength (0)];
		double[] G = g(vec);
		for (int i = 0; i < vec.GetLength (0); ++i) {
			if (vec [i].Equals (l[i])) {
				tempPhi [i] = 0.0;
			} else {
				tempPhi [i] = G [i];
			}
		}
		return tempPhi;
	}

	protected double[] B(double[] vec) {
		double[] tempB = new double[vec.GetLength (0)];
		double[] G = g(vec);
		for (int i = 0; i < vec.GetLength (0); ++i) {
			if (vec [i].Equals (l [i])) {
				tempB[i] = Math.Min(G [i], 0.0);
			} else {
				tempB[i] = 0.0;
			}
		}
		return tempB;
	}

	protected double[] v(double[] vec) {
		return PlusMinusVec (phi(vec), B(vec), true);
	}

	protected double[] phiTilde(double[] vec) {
		double[] tempPhiTilde = new double[vec.GetLength (0)];
		double[] Phi = phi(vec);
		for (int i = 0; i < vec.GetLength(0); ++i) {
			tempPhiTilde[i] = Math.Min((vec [i] - l [i]) / alphaBar, Phi[i]);
		}
		return tempPhiTilde;
	}
		
	protected double[] projection(double[] vec) {
		double[] diff = PlusMinusVec (vec, l, false);
		for (int i = 0; i < diff.GetLength (0); ++i) {
			diff [i] = Math.Max (diff [i], 0.0);
		} 
		return PlusMinusVec (l, diff, true); 
	}

	protected double calcAlphaF(double[] p) {
		double alphaF = 100000f; //Large nr(double)(Mathf.Infinity);
		for (int i = 0; i < x.GetLength(0); i++) {
			if (p[i] > 0) {
				double temp = (x[i] - l[i])/p[i];
				if (temp < alphaF) {
					alphaF = temp;
				}
			}
		}
		return alphaF;
	}

	protected double frobeniusNormV(double[] vec) {
		double norm = 0.0;
		for (int i = 0; i < vec.GetLength (0); ++i) {
			norm += Math.Pow(vec[i], 2);
		}
		return Math.Sqrt (norm);
	}

	protected double frobeniusNormM() {
		double norm = 0.0;
		for (int i = 0; i < A.Count; ++i) {
				for (int j = 0; j < A[i].Count; ++j) {
						norm += Math.Pow(A[i][j].value, 2);
				}
		}
		return Math.Sqrt (norm);
	}
		
	protected double DotProduct(double[] vec1, double[] vec2){
		double tVal = 0;
		for (int x = 0; x < vec1.Length; x++){
			tVal += vec1[x] * vec2[x];
		}
		return tVal;
	}

	protected double[] TwoMulOne(double[] vec) {
		double[] res = new double[vec.GetLength (0)];
		for (int i = 0; i < A.Count; ++i) {
			res [i] = OneMultOne (i, vec);
		}
		return res;
	}

	protected double OneMultOne(int i, double[] vec) {
		double res = 0;
		for(int j = 0; j < A[i].Count; ++j) {
			res += A[i][j].value * vec[A[i][j].colIndex];
		}
		return res;
	}
	protected double[] scalarMult(double scalar, double[] vec) {
		for (int i = 0; i < vec.GetLength (0); ++i) {
			vec [i] *= scalar;
		}
		return vec;
	}

	protected double[] PlusMinusVec(double[] a, double[] b, bool plus) {
		double op = 1.0;
		if (!plus)
			op = -1.0;
		double[] res = new double[a.GetLength (0)];
		for (int i = 0; i < a.GetLength (0); ++i) {
			res [i] = a [i] + op*b [i];
		}
		return res;
	}
}
