using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct CellCoefficients
{
    public double self;
    public double north;
    public double south;
    public double west;
    public double east;
}

[BurstCompile]
public struct LcpMatrixAssemblyJob : IJobParallelFor
{
    [ReadOnly] public int nCellsX;
    [ReadOnly] public int nCellsZ;
    [ReadOnly] public float dt;
    [ReadOnly] public float cellSize;
    [ReadOnly] public float maxDensity;
    [ReadOnly] public bool clamped;
    
    [ReadOnly] public NativeArray<float> density;
    [ReadOnly] public NativeArray<float> availableArea;
    [ReadOnly] public NativeArray<float> xEdgeDensity;
    [ReadOnly] public NativeArray<float> zEdgeDensity;
    [ReadOnly] public NativeArray<float> xEdgeVelocity;
    [ReadOnly] public NativeArray<float> zEdgeVelocity;

    [WriteOnly] public NativeArray<CellCoefficients> outCoeffs;
    [WriteOnly] public NativeArray<double> outB;

    public void Execute(int index)
    {
        int i = index / nCellsX;
        int j = index % nCellsX;
        double cachedCellSizeSquared = cellSize * cellSize;

        float xEdgeDensity_Right = xEdgeDensity[i * (nCellsX + 1) + j + 1];
        float xEdgeVelocity_Right = xEdgeVelocity[i * (nCellsX + 1) + j + 1];
        float xEdgeDensity_Left = xEdgeDensity[i * (nCellsX + 1) + j];
        float xEdgeVelocity_Left = xEdgeVelocity[i * (nCellsX + 1) + j];

        float zEdgeDensity_Upper = zEdgeDensity[(i + 1) * nCellsX + j];
        float zEdgeVelocity_Upper = zEdgeVelocity[(i + 1) * nCellsX + j];
        float zEdgeDensity_Lower = zEdgeDensity[i * nCellsX + j];
        float zEdgeVelocity_Lower = zEdgeVelocity[i * nCellsX + j];

        double temp = availableArea[index] * maxDensity - density[index]
            + ((xEdgeDensity_Right * xEdgeVelocity_Right
            + zEdgeDensity_Upper * zEdgeVelocity_Upper
            - xEdgeDensity_Left * xEdgeVelocity_Left
            - zEdgeDensity_Lower * zEdgeVelocity_Lower) / cellSize) * dt;

        if (availableArea[index] < 0.65f)
        {
            temp = availableArea[index] * maxDensity;
        }
        else if (clamped && temp < 0.0)
        {
            temp = 0.0;
        }
        outB[index] = temp;

        CellCoefficients cc = new CellCoefficients();
        
        cc.self = (double)(dt * (xEdgeDensity_Left + xEdgeDensity_Right + zEdgeDensity_Lower + zEdgeDensity_Upper)) / cachedCellSizeSquared;

        if (i > 0)
        {
            cc.north = -(dt * zEdgeDensity_Lower / cachedCellSizeSquared);
        }
        
        if (j > 0)
        {
            cc.west = -(dt * xEdgeDensity_Left / cachedCellSizeSquared);
        }

        if (i < nCellsZ - 1)
        {
            cc.south = -(dt * zEdgeDensity_Upper / cachedCellSizeSquared);
        }

        if (j < nCellsX - 1)
        {
            cc.east = -(dt * xEdgeDensity_Right / cachedCellSizeSquared);
        }

        outCoeffs[index] = cc;
    }
}

[BurstCompile]
public struct MPRGPSolverJob : IJob
{
    [ReadOnly] public int nCellsX;
    [ReadOnly] public int nCellsZ;
    [ReadOnly] public int maxIterations;
    [ReadOnly] public double solverEpsilon;
    
    [ReadOnly] public NativeArray<CellCoefficients> coeffs;
    [ReadOnly] public NativeArray<double> b;
    [ReadOnly] public NativeArray<double> l;
    
    public NativeArray<double> x;

    private double OneMultOne(int i, ref NativeArray<double> vec)
    {
        double val = coeffs[i].self * vec[i];
        int row = i / nCellsX;
        int col = i % nCellsX;
        if (row > 0) val += coeffs[i].north * vec[i - nCellsX];
        if (row < nCellsZ - 1) val += coeffs[i].south * vec[i + nCellsX];
        if (col > 0) val += coeffs[i].west * vec[i - 1];
        if (col < nCellsX - 1) val += coeffs[i].east * vec[i + 1];
        return val;
    }

    private void TwoMulOne(ref NativeArray<double> vec, ref NativeArray<double> res)
    {
        for (int i = 0; i < x.Length; ++i)
        {
            res[i] = OneMultOne(i, ref vec);
        }
    }

    private double DotProduct(ref NativeArray<double> v1, ref NativeArray<double> v2)
    {
        double sum = 0.0;
        for (int i = 0; i < v1.Length; ++i)
        {
            sum += v1[i] * v2[i];
        }
        return sum;
    }

    private double FrobeniusNormV(ref NativeArray<double> vec)
    {
        double norm = 0.0;
        for (int i = 0; i < vec.Length; ++i)
        {
            norm += vec[i] * vec[i];
        }
        return math.sqrt(norm);
    }

    private double FrobeniusNormM()
    {
        double norm = 0.0;
        for (int i = 0; i < coeffs.Length; ++i)
        {
            norm += coeffs[i].self * coeffs[i].self;
            int row = i / nCellsX;
            int col = i % nCellsX;
            if (row > 0) norm += coeffs[i].north * coeffs[i].north;
            if (row < nCellsZ - 1) norm += coeffs[i].south * coeffs[i].south;
            if (col > 0) norm += coeffs[i].west * coeffs[i].west;
            if (col < nCellsX - 1) norm += coeffs[i].east * coeffs[i].east;
        }
        return math.sqrt(norm);
    }

    public void Execute()
    {
        int totalCells = x.Length;
        const double smallEpsilon = 1e-15;

        NativeArray<double> r = new NativeArray<double>(totalCells, Allocator.Temp);
        NativeArray<double> p = new NativeArray<double>(totalCells, Allocator.Temp);
        NativeArray<double> Ap = new NativeArray<double>(totalCells, Allocator.Temp);
        NativeArray<double> y = new NativeArray<double>(totalCells, Allocator.Temp);
        NativeArray<double> phiVal = new NativeArray<double>(totalCells, Allocator.Temp);
        NativeArray<double> BVal = new NativeArray<double>(totalCells, Allocator.Temp);
        NativeArray<double> phiTildeVal = new NativeArray<double>(totalCells, Allocator.Temp);
        NativeArray<double> vVal = new NativeArray<double>(totalCells, Allocator.Temp);
        NativeArray<double> tempAx = new NativeArray<double>(totalCells, Allocator.Temp);

        double alphaBar = 1.0 / (2.0 * FrobeniusNormM() + smallEpsilon);
        double epsilon = solverEpsilon * FrobeniusNormV(ref b);
        if (epsilon > 0.01) epsilon = 0.001;

        TwoMulOne(ref x, ref tempAx);
        for (int i = 0; i < totalCells; i++)
        {
            r[i] = tempAx[i] + b[i];
        }

        UpdateAuxVectors(ref x, ref r, alphaBar, ref phiVal, ref BVal, ref phiTildeVal, ref vVal);

        for (int i = 0; i < totalCells; i++)
        {
            p[i] = phiVal[i];
        }

        double gammaConstant = 1.0;
        int cnt = 0;

        while (FrobeniusNormV(ref vVal) > epsilon && cnt < maxIterations && !CheckEndCondition(ref r, ref x, epsilon))
        {
            cnt++;
            double normB = FrobeniusNormV(ref BVal);
            double phiTildeDotPhi = DotProduct(ref phiTildeVal, ref phiVal);

            if ((normB * normB) <= gammaConstant * phiTildeDotPhi)
            {
                TwoMulOne(ref p, ref Ap);
                double pAp = DotProduct(ref p, ref Ap);
                double alphaCG = DotProduct(ref r, ref p) / (pAp + smallEpsilon);

                for (int i = 0; i < totalCells; i++)
                {
                    y[i] = x[i] - alphaCG * p[i];
                }

                double alphaF = CalcAlphaF(ref p);
                if (alphaCG <= alphaF)
                {
                    for (int i = 0; i < totalCells; i++)
                    {
                        x[i] = y[i];
                        r[i] = r[i] - alphaCG * Ap[i];
                    }

                    UpdateAuxVectors(ref x, ref r, alphaBar, ref phiVal, ref BVal, ref phiTildeVal, ref vVal);

                    double gamma = DotProduct(ref phiVal, ref Ap) / (pAp + smallEpsilon);

                    for (int i = 0; i < totalCells; i++)
                    {
                        p[i] = phiVal[i] - gamma * p[i];
                    }
                }
                else
                {
                    for (int i = 0; i < totalCells; i++)
                    {
                        x[i] = x[i] - alphaF * p[i];
                        r[i] = r[i] - alphaF * Ap[i];
                    }

                    UpdateAuxVectors(ref x, ref r, alphaBar, ref phiVal, ref BVal, ref phiTildeVal, ref vVal);

                    for (int i = 0; i < totalCells; i++)
                    {
                        double val = x[i] - alphaBar * phiVal[i];
                        x[i] = math.max(val, l[i]);
                    }

                    TwoMulOne(ref x, ref tempAx);
                    for (int i = 0; i < totalCells; i++)
                    {
                        r[i] = tempAx[i] + b[i];
                    }

                    UpdateAuxVectors(ref x, ref r, alphaBar, ref phiVal, ref BVal, ref phiTildeVal, ref vVal);

                    for (int i = 0; i < totalCells; i++)
                    {
                        p[i] = phiVal[i];
                    }
                }
            }
            else
            {
                TwoMulOne(ref BVal, ref Ap);
                double dAd = DotProduct(ref BVal, ref Ap);
                double alphaCG = DotProduct(ref r, ref BVal) / (dAd + smallEpsilon);

                for (int i = 0; i < totalCells; i++)
                {
                    x[i] = x[i] - alphaCG * BVal[i];
                    r[i] = r[i] - alphaCG * Ap[i];
                }

                UpdateAuxVectors(ref x, ref r, alphaBar, ref phiVal, ref BVal, ref phiTildeVal, ref vVal);

                for (int i = 0; i < totalCells; i++)
                {
                    p[i] = phiVal[i];
                }
            }
        }

        r.Dispose();
        p.Dispose();
        Ap.Dispose();
        y.Dispose();
        phiVal.Dispose();
        BVal.Dispose();
        phiTildeVal.Dispose();
        vVal.Dispose();
        tempAx.Dispose();
    }

    private void UpdateAuxVectors(ref NativeArray<double> xVec, ref NativeArray<double> rVec, double alphaBar,
                                   ref NativeArray<double> phiVal, ref NativeArray<double> BVal,
                                   ref NativeArray<double> phiTildeVal, ref NativeArray<double> vVal)
    {
        for (int i = 0; i < xVec.Length; i++)
        {
            double gi = rVec[i];

            if (xVec[i] <= l[i] + 1e-30)
            {
                phiVal[i] = 0.0;
            }
            else
            {
                phiVal[i] = gi;
            }

            if (xVec[i] <= l[i] + 1e-30)
            {
                BVal[i] = math.min(gi, 0.0);
            }
            else
            {
                BVal[i] = 0.0;
            }

            vVal[i] = phiVal[i] - BVal[i];
            phiTildeVal[i] = math.min((xVec[i] - l[i]) / alphaBar, phiVal[i]);
        }
    }

    private double CalcAlphaF(ref NativeArray<double> pVec)
    {
        double alphaF = 100000.0;
        for (int i = 0; i < x.Length; i++)
        {
            if (pVec[i] > 0.0)
            {
                double temp = (x[i] - l[i]) / pVec[i];
                if (temp < alphaF)
                {
                    alphaF = temp;
                }
            }
        }
        return alphaF;
    }

    private bool CheckEndCondition(ref NativeArray<double> rVec, ref NativeArray<double> xVec, double eps)
    {
        bool conditionA = true;
        bool conditionB = true;
        double sum = 0.0;

        for (int i = 0; i < rVec.Length; ++i)
        {
            if (rVec[i] < 0.0)
                conditionA = false;
            if (xVec[i] < 0.0)
                conditionB = false;
            sum += rVec[i] * xVec[i];
        }
        bool conditionC = math.abs(sum) < eps;
        return conditionA && conditionB && conditionC;
    }
}

[BurstCompile]
public struct PSORSolverJob : IJob
{
    [ReadOnly] public int nCellsX;
    [ReadOnly] public int nCellsZ;
    [ReadOnly] public int maxIterations;
    [ReadOnly] public double solverEpsilon;
    
    [ReadOnly] public NativeArray<CellCoefficients> coeffs;
    [ReadOnly] public NativeArray<double> b;
    [ReadOnly] public NativeArray<double> l;
    
    public NativeArray<double> x;

    private double OneMultOne(int i, ref NativeArray<double> vec)
    {
        double val = coeffs[i].self * vec[i];
        int row = i / nCellsX;
        int col = i % nCellsX;
        if (row > 0) val += coeffs[i].north * vec[i - nCellsX];
        if (row < nCellsZ - 1) val += coeffs[i].south * vec[i + nCellsX];
        if (col > 0) val += coeffs[i].west * vec[i - 1];
        if (col < nCellsX - 1) val += coeffs[i].east * vec[i + 1];
        return val;
    }

    private bool CheckEndCondition(ref NativeArray<double> rVec, ref NativeArray<double> xVec, double eps)
    {
        bool conditionA = true;
        bool conditionB = true;
        double sum = 0.0;

        for (int i = 0; i < rVec.Length; ++i)
        {
            if (rVec[i] < 0.0)
                conditionA = false;
            if (xVec[i] < 0.0)
                conditionB = false;
            sum += rVec[i] * xVec[i];
        }
        return conditionA && conditionB && (math.abs(sum) < eps);
    }

    public void Execute()
    {
        int totalCells = x.Length;
        double delta = 1.3;
        NativeArray<double> rVec = new NativeArray<double>(totalCells, Allocator.Temp);

        for (int k = 0; k < maxIterations; ++k)
        {
            for (int i = 0; i < totalCells; i++)
            {
                double val = coeffs[i].self * x[i];
                int r = i / nCellsX;
                int c = i % nCellsX;
                if (r > 0) val += coeffs[i].north * x[i - nCellsX];
                if (r < nCellsZ - 1) val += coeffs[i].south * x[i + nCellsX];
                if (c > 0) val += coeffs[i].west * x[i - 1];
                if (c < nCellsX - 1) val += coeffs[i].east * x[i + 1];
                rVec[i] = val + b[i];
            }

            if (CheckEndCondition(ref rVec, ref x, solverEpsilon))
            {
                break;
            }

            double oldXMax = 0.0;
            double newXMax = 0.0;

            for (int i = 0; i < totalCells; ++i)
            {
                oldXMax = math.max(oldXMax, x[i]);
                double aSelf = coeffs[i].self;
                if (math.abs(aSelf) > solverEpsilon)
                {
                    double xVal = x[i] - delta * (OneMultOne(i, ref x) + b[i]) / aSelf;
                    x[i] = math.max(0.0, xVal);
                }
                newXMax = math.max(newXMax, x[i]);
            }

            if (math.abs(oldXMax - newXMax) < solverEpsilon)
            {
                break;
            }
        }

        rVec.Dispose();
    }
}

[BurstCompile]
public struct SolveXEdgesJob : IJobParallelFor
{
    [ReadOnly] public int nCellsX;
    [ReadOnly] public int nCellsZ;
    [ReadOnly] public float cellSize;
    [ReadOnly] public NativeArray<double> xArray;

    public NativeArray<float> xEdgeVelocity;

    public void Execute(int index)
    {
        int cellRow = index / (nCellsX + 1);
        int cellCol = index % (nCellsX + 1);
        
        float pressureGradient = 0f;
        int cellIndex = cellRow * nCellsX + cellCol;

        if (cellCol == 0)
        {
            pressureGradient = (float)(xArray[cellIndex] - 0.0) / cellSize;
        }
        else if (cellCol == nCellsX)
        {
            pressureGradient = (float)(0.0 - xArray[cellIndex - 1]) / cellSize;
        }
        else
        {
            pressureGradient = (float)(xArray[cellIndex] - xArray[cellIndex - 1]) / cellSize;
        }

        xEdgeVelocity[index] = xEdgeVelocity[index] - pressureGradient;
    }
}

[BurstCompile]
public struct SolveZEdgesJob : IJobParallelFor
{
    [ReadOnly] public int nCellsX;
    [ReadOnly] public int nCellsZ;
    [ReadOnly] public float cellSize;
    [ReadOnly] public NativeArray<double> xArray;

    public NativeArray<float> zEdgeVelocity;

    public void Execute(int index)
    {
        int cellRow = index / nCellsX;
        int cellCol = index % nCellsX;
        
        float pressureGradient = 0f;
        int cellIndex = cellRow * nCellsX + cellCol;

        if (cellRow == 0)
        {
            pressureGradient = (float)(xArray[cellIndex] - 0.0) / cellSize;
        }
        else if (cellRow == nCellsZ)
        {
            pressureGradient = (float)(0.0 - xArray[cellIndex - nCellsX]) / cellSize;
        }
        else
        {
            pressureGradient = (float)(xArray[cellIndex] - xArray[cellIndex - nCellsX]) / cellSize;
        }

        zEdgeVelocity[index] = zEdgeVelocity[index] - pressureGradient;
    }
}

[BurstCompile]
public struct RenormalizeXEdgesJob : IJobParallelFor
{
    [ReadOnly] public float agentMaxSpeed;
    
    public NativeArray<float3> xEdgeVelocityVectors;
    public NativeArray<float> xEdgeVelocity;

    public void Execute(int index)
    {
        float3 velVec = xEdgeVelocityVectors[index];
        float len = math.length(velVec);
        if (len > 1e-6f)
        {
            velVec = (velVec / len) * agentMaxSpeed;
        }
        else
        {
            velVec = float3.zero;
        }
        xEdgeVelocityVectors[index] = velVec;
        xEdgeVelocity[index] = velVec.x;
    }
}

[BurstCompile]
public struct RenormalizeZEdgesJob : IJobParallelFor
{
    [ReadOnly] public float agentMaxSpeed;
    
    public NativeArray<float3> zEdgeVelocityVectors;
    public NativeArray<float> zEdgeVelocity;

    public void Execute(int index)
    {
        float3 velVec = zEdgeVelocityVectors[index];
        float len = math.length(velVec);
        if (len > 1e-6f)
        {
            velVec = (velVec / len) * agentMaxSpeed;
        }
        else
        {
            velVec = float3.zero;
        }
        zEdgeVelocityVectors[index] = velVec;
        zEdgeVelocity[index] = velVec.z;
    }
}
