using System.Diagnostics;
using System.Collections.Generic;
using System;

public class LCPSolverMIC : LCPSolver {


	public double[] LCPSolver(List<List<LCPSolver.denseMatrixNode>> aList, double[,] matrixA, double[] bArray, double[] xArray, double[] lArray) {
		this.A = aList; this.b = bArray; this.x = xArray; this.l = lArray;
		double[,] M = getM (matrixA);

		gammaConstant = 1; //Around 1
		alphaBar = 1 / (2*frobeniusNormM() + smallEpsilon);
		epsilon = SimulationGrid.instance.solverEpsilon * frobeniusNormV (b); //Very very small..
		if (epsilon > 0.01)
			epsilon = 0.001;

		//		UnityEngine.Debug.Log ("Epsilon: " + epsilon);

		double[] g = PlusMinusVec(TwoMulOne(x), b, true); //Ax+b (Dostal says Ax-b)
		double[] z = MMult(M, g);
		double[] p = z;
		double alphaCG;
		int cnt = 0;
		int lim = SimulationGrid.instance.solverMaxIterations;
		Stopwatch s = new Stopwatch ();
		s.Start ();
		while (frobeniusNormV (v(x)) > epsilon && cnt < lim && !checkEndCondition()) {
			cnt += 1;
			double normB = frobeniusNormV (B(x));
			if ((normB * normB) <= gammaConstant * DotProduct (phiTilde(x), phi(x))) {
				//1. Trial Conjugate Gradient Step
				double[] Ap = TwoMulOne (p);
				alphaCG = DotProduct (z, g) / (DotProduct (p, Ap) + smallEpsilon);

				double[] y = PlusMinusVec (x, scalarMult (alphaCG, p), false);
				double alphaF = calcAlphaF (p);
				if (alphaCG <= alphaF) {
					//2. Conjugate Gradient Step

					x = y;
					g = PlusMinusVec (g, scalarMult (alphaCG, Ap), false);
					z = MMult (M, g);
					double gamma = DotProduct (z, Ap) /(DotProduct (p, Ap) + smallEpsilon);

					p = PlusMinusVec (z, scalarMult (gamma, p), false);
				} else {
					//3. Expansion Step
					x = PlusMinusVec (x, scalarMult(alphaF, p), false);
					g = PlusMinusVec (g, scalarMult (alphaF, Ap), false); //Why do this..?
					x = projection (PlusMinusVec (x, scalarMult (alphaBar, phi(x)), false));
					g = PlusMinusVec (TwoMulOne(x), b, true); //Dostal says Ax-b
					z = MMult(M, g);
					p = z;
				}
			} else {
				//4. Proportioning Step
				double[] d = B(x);
				double[] Ad = TwoMulOne (d);
				alphaCG = DotProduct (g, d) / (DotProduct (d, Ad) + smallEpsilon);
				x = PlusMinusVec (x, scalarMult(alphaCG, d), false);
				g = PlusMinusVec (g, scalarMult(alphaCG, Ad), false);
				z = MMult (M, g);
				p = z;
			}
		}	
		//UnityEngine.Debug.Log ("Took : " + s.ElapsedMilliseconds + " ms" );
		if (cnt == lim)
			UnityEngine.Debug.Log ("Count reached");
		for (int i = 0; i < x.GetLength (0); ++i) {
			if (Double.IsNaN (x [i])) {
				UnityEngine.Debug.Log ("IS NAN!!");
			}
		}
		return x;
	}

	internal double[] MMult(double[,] M, double[] g) {
		double[] res = new double[g.GetLength (0)];
		for (int i = 0; i < res.GetLength (0); ++i) {
			res [i] = M [i, i] * g [i];
		}
		return res;
	}

	internal double[,] getE(double[,] A) {
		int len = (int)Math.Sqrt (A.GetLength (0));
		double[,] e = new double[A.GetLength (0), A.GetLength (0)];
		e [0, 0] = A [0, 0];
		//We want to iterate len*len times.. Remove len?
		for (int i = 0; i < len; ++i) {
			for (int j = 0; j < len; ++j) {
				double tmpe = A [i * len + j, i * len + j];
				if (i > 0) 
					tmpe -= Math.Pow(A[(i-1)*len+j, i*len+j]/(e[(i-1)*len+j,(i-1)*len+j] + Math.Pow(10, -30)), 2);
				if (j > 0)
					tmpe -= Math.Pow(A[i*len+(j-1), i*len+j]/(e[i*len+(j-1),i*len+(j-1)] + Math.Pow(10, -30)), 2);
				if (i > 0 && j < len - 1)
					tmpe -= A [(i - 1) * len + j, i * len + j] * A [(i - 1) * len + j, (i - 1) * len + (j + 1)] / Math.Pow ((e [(i - 1) * len + j, (i - 1) * len + j] + Math.Pow (10, -30)), 2);
				if (j > 0 && i < len - 1)
					tmpe -= A [i * len + (j-1), i * len + j] * A [i * len + (j-1), (i + 1) * len + (j - 1)] / Math.Pow ((e [i * len + (j-1), i * len + (j-1)] + Math.Pow (10, -30)), 2);
				tmpe = Math.Sqrt (tmpe);
				e [i * len + j, i * len + j] = tmpe;
			}
		}
		return e;
	}

	internal double[,] getM(double[,] A) {
		double[,] E = getE (A);
		double[,] L = new double[A.GetLength (0), A.GetLength (0)];
		//O(cell*cell)
		L[0, 0] = Math.Abs(E[0, 0]) > 0 ? A[0, 0]* (1.0/E[0, 0]) + E[0, 0] : 0;
		for (int i = 1; i < A.GetLength (0); ++i) {
			for (int j = i-1; j <= i; ++j) {
				L[i, j] = Math.Abs (E [i, j]) > 0 ?  A [i, j] * (1.0 / E [i, j]) + E [i, j] : 0;
			}
		}
		//M is diag matrix..
		//L is M now
		//ADDITION: Inverse!
		L[0, 0] *= L[0, 0];
		for (int i = 1; i < A.GetLength (0); ++i) {
			L [i, i] = Math.Pow (L [i, i - 1], 2) + Math.Pow (L [i, i], 2);
			L [i, i] = Math.Abs (L [i, i]) > 0 ? 1.0 / L [i, i] : 0;
		}
		return L;
	}


		
}
