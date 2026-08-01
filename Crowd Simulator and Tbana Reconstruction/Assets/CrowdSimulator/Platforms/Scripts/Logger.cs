using UnityEngine;
using System.IO;
using System.Text;
using System.Globalization;

public class Logger : MonoBehaviour
{
    internal string fileNameMasterSummary = "master_summary_log.csv";
    internal string fileNameMasterDensity = "master_density_time_series.csv";
    internal string fileNameWarningLog = "warning_log.csv";
    internal string fileNameYellowLineLog = "yellow_line_log.csv";
    internal string fileNameAgentMetrics = "master_agent_metrics.csv";

    private Main main;
    private TrainController trainController;
    private TestController testController;

    // Internal state
    private string filePathMasterSummary;
    private string filePathMasterDensity;
    private string filePathWarningLog;
    private string filePathYellowLineLog;
    private string filePathAgentMetrics;
    private string scenarioHeader;
    internal string scenarioPrefix;
    internal string batchFolderPath;

    private StreamWriter summaryWriter;
    private StreamWriter densityTimeSeriesWriter;
    private StreamWriter yellowLineWriter;
    private StreamWriter warningWriter;
    private StreamWriter agentMetricsWriter;
    private System.Collections.Generic.List<string> bufferedYellowLines = new System.Collections.Generic.List<string>();
    private System.Collections.Generic.List<string> bufferedDensities = new System.Collections.Generic.List<string>();
    private System.Collections.Generic.List<string> bufferedAgentMetrics = new System.Collections.Generic.List<string>();

    internal float[] alightingStartTime = new float[2];
    internal float[] boardingStartTime = new float[2];
    internal float[] alightingEndTime = new float[2];
    internal float[] boardingEndTime = new float[2];
    internal float[] allAlightersExitedTimestamp = new float[2];
    internal int nYellowLineOversteps = 0;

    // Agent metrics
    internal float[,] totalTravelTime = new float[2, 2];
    internal float[,] totalDistance = new float[2, 2];
    internal float[,] totalPathEfficiency = new float[2, 2];
    internal int[,] totalAgents = new int[2, 2];
    internal float[,] totalSpeed = new float[2, 2];
    internal float[,] totalEntityDensity = new float[2,2];
    internal float[,] totalSocialProximity = new float[2,2];

    
    void Start()
    {
        main = FindObjectOfType<Main>();
        if (main == null)
        {
            Debug.LogError("Main script not found in the scene.");
            return;
        }
        trainController = FindObjectOfType<TrainController>();
        if (trainController == null) {
            Debug.LogError("TrainController not found in the scene.");
            return;
        }
        testController = FindObjectOfType<TestController>();
        if (testController == null)
        {
            Debug.LogError("Logger did not find TestController script.");
            return;
        }

        if (!testController.log)
        {
            return;
        }

        fileNameMasterSummary = $"master_summary.csv";
        fileNameMasterDensity = $"master_density_time_series.csv";
        fileNameWarningLog = $"warning_log.csv";
        fileNameYellowLineLog = $"yellow_line_log.csv";
        fileNameAgentMetrics = $"master_agent_metrics.csv";

        // Determine batch folder path
        int repIndex = testController != null ? testController.repetitionIndex : 0;
        string batchFolderName = "";
        if (RunManager.Instance != null)
        {
            batchFolderName = $"realBatch{RunManager.Instance.BaseBatchNumber + repIndex}";
            batchFolderPath = Path.Combine(Application.persistentDataPath, batchFolderName);
        }
        else
        {
            // Fallback if running scene directly in Editor without RunManager
            int nextBatchNum = RunManager.GetNextBatchNumber();
            batchFolderName = $"realBatch{nextBatchNum}";
            batchFolderPath = Path.Combine(Application.persistentDataPath, batchFolderName);
        }

        // Resolve absolute paths
        filePathMasterSummary = Path.Combine(batchFolderPath, fileNameMasterSummary);
        filePathMasterDensity = Path.Combine(batchFolderPath, fileNameMasterDensity);
        filePathWarningLog = Path.Combine(batchFolderPath, fileNameWarningLog);
        filePathYellowLineLog = Path.Combine(batchFolderPath, fileNameYellowLineLog);
        filePathAgentMetrics = Path.Combine(batchFolderPath, fileNameAgentMetrics);

        EnsureDirectory(filePathMasterSummary);
        EnsureDirectory(filePathMasterDensity);
        EnsureDirectory(filePathWarningLog);
        EnsureDirectory(filePathYellowLineLog);
        EnsureDirectory(filePathAgentMetrics);

        scenarioHeader = "RunID,Platform,Scenario,FlowType,EntryFlowTotal,ExitFlowTotal,AlightBeforeBoarding,EntryFlowT1,EntryFlowT2,ExitFlowT1,ExitFlowT2";
        scenarioPrefix = string.Join(",",
            testController.runIndex.ToString(),
            trainController.platformType.ToString(),
            testController.scenario.ToString(),
            testController.flowType.ToString(),
            testController.entryFlow.ToString(),
            testController.exitFlow.ToString(),
            testController.alightBeforeBoarding.ToString(),
            testController.entryFlowLines[0].ToString(),
            testController.entryFlowLines[1].ToString(),
            testController.exitFlowLines[0].ToString(),
            testController.exitFlowLines[1].ToString()
        );

        summaryWriter = OpenWriter(filePathMasterSummary, true);
        densityTimeSeriesWriter = OpenWriter(filePathMasterDensity, true);
        warningWriter = OpenWriter(filePathWarningLog, true);
        yellowLineWriter = OpenWriter(filePathYellowLineLog, true);
        agentMetricsWriter = OpenWriter(filePathAgentMetrics, true);

        if (summaryWriter != null && new FileInfo(filePathMasterSummary).Length == 0) WriteHeaderMasterSummary();
        if (densityTimeSeriesWriter != null && new FileInfo(filePathMasterDensity).Length == 0) WriteHeaderMasterDensity();
        if (warningWriter != null && new FileInfo(filePathWarningLog).Length == 0) WriteHeaderWarningLog();
        if (yellowLineWriter != null && new FileInfo(filePathYellowLineLog).Length == 0) WriteHeaderYellowLineLog();
        if (agentMetricsWriter != null && new FileInfo(filePathAgentMetrics).Length == 0) WriteHeaderAgentMetrics();
    }

    public void CloseAllWriters()
    {
        FlushAndClose(ref summaryWriter, "Master Summary");
        FlushAndClose(ref densityTimeSeriesWriter, "Master Density Time Series");
        FlushAndClose(ref warningWriter, "Warning Log");
        FlushAndClose(ref yellowLineWriter, "Yellow Line Log");
        FlushAndClose(ref agentMetricsWriter, "Agent Metrics");
    }

    private void EnsureDirectory(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private StreamWriter OpenWriter(string filePath, bool append)
    {
        try
        {
            return new StreamWriter(filePath, append, Encoding.UTF8); 
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Logger] Failed to open file '{filePath}': {e.Message}");
            return null;
        }
    }

    private void FlushAndClose(ref StreamWriter writer, string label)
    {
        if (writer == null) return;
        try
        {
            writer.Flush();
            writer.Close();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Logger] Error closing {label} writer: {e.Message}");
        }
        writer = null;
    }

    private void WriteHeaderMasterSummary()
    {
        if (summaryWriter == null) return;

        StringBuilder header = new StringBuilder();
        header.Append(scenarioHeader + ",");
        
        // Trainline 1 specific metrics
        header.Append("AlightingTime_T1,BoardingTime_T1,TotalBAT_T1,AllAlightersExitTime_T1," + 
        "AvgTravelTime_T1_boarding,AvgTravelTime_T1_alighting,"+
        "AvgDistance_T1_boarding,AvgDistance_T1_alighting,"+
        "AvgSpeed_T1_boarding,AvgSpeed_T1_alighting,"+
        "AvgPathEfficiency_T1_boarding,AvgPathEfficiency_T1_alighting,"+
        "AvgEntityDensity_T1_boarding,AvgEntityDensity_T1_alighting,"+
        "AvgSocialProximity_T1_boarding,AvgSocialProximity_T1_alighting,"+
        "TotalAgents_T1_boarding,TotalAgents_T1_alighting,");
        
        // Trainline 2 specific metrics
        header.Append("AlightingTime_T2,BoardingTime_T2,TotalBAT_T2,AllAlightersExitTime_T2," +
        "AvgTravelTime_T2_boarding,AvgTravelTime_T2_alighting,"+
        "AvgDistance_T2_boarding,AvgDistance_T2_alighting,"+
        "AvgSpeed_T2_boarding,AvgSpeed_T2_alighting,"+
        "AvgPathEfficiency_T2_boarding,AvgPathEfficiency_T2_alighting,"+
        "AvgEntityDensity_T2_boarding,AvgEntityDensity_T2_alighting,"+
        "AvgSocialProximity_T2_boarding,AvgSocialProximity_T2_alighting,"+
        "TotalAgents_T2_boarding,TotalAgents_T2_alighting,");
        
        // Run-wide global metrics
        header.Append("TimeClearPlatformTotal,"+
        "AvgTravelTime_boarding,AvgTravelTime_alighting,"+
        "AvgDistance_boarding,AvgDistance_alighting,"+
        "AvgSpeed_boarding,AvgSpeed_alighting,"+
        "AvgPathEfficiency_boarding,AvgPathEfficiency_alighting,"+
        "AvgEntityDensity_boarding,AvgEntityDensity_alighting,"+
        "AvgSocialProximity_boarding,AvgSocialProximity_alighting,"+
        "TotalAgents_boarding,TotalAgents_alighting,"+
        "YellowLineOverstepsTotal");
        
        summaryWriter.WriteLine(header.ToString());
    }

    private void WriteHeaderMasterDensity()
    {
        if (densityTimeSeriesWriter == null) return;
        StringBuilder header = new StringBuilder(scenarioHeader + ",TimeStamp,Platform1,Platform2,MiddlePlatform");
        densityTimeSeriesWriter.WriteLine(header.ToString());
    }

    private void WriteHeaderWarningLog()
    {
        if (warningWriter == null) return;
        StringBuilder header = new StringBuilder(scenarioHeader + ",TimeStamp,WarningMessage");
        warningWriter.WriteLine(header.ToString());
    }

    private void WriteHeaderYellowLineLog()
    {
        if (yellowLineWriter == null) return;
        StringBuilder header = new StringBuilder(scenarioHeader + ",TimeStamp,PassengerType,PositionX,PositionZ");
        yellowLineWriter.WriteLine(header.ToString());
    }

    public void LogWarning(string warningMessage)
    {
        if (warningWriter == null) return;

        StringBuilder line = new StringBuilder();
        line.Append(scenarioPrefix + ",");
        line.Append(main.simulationTime.ToString("F2", CultureInfo.InvariantCulture) + ",");
        line.Append(warningMessage);
        warningWriter.WriteLine(line.ToString());
    }

    public void LogYellowLineViolation(Vector3 position, TrainController.AgentType passengerType)
    {
        StringBuilder line = new StringBuilder();
        line.Append(scenarioPrefix + ",");
        line.Append(main.simulationTime.ToString("F2", CultureInfo.InvariantCulture) + ",");
        line.Append(passengerType.ToString() + ",");
        line.Append(position.x.ToString("F2", CultureInfo.InvariantCulture) + ",");
        line.Append(position.z.ToString("F2", CultureInfo.InvariantCulture));
        bufferedYellowLines.Add(line.ToString());
    }

    public void LogDensity(string densityValues)
    {
        StringBuilder line = new StringBuilder();
        line.Append(scenarioPrefix + ",");
        line.Append(main.simulationTime.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(densityValues);
        bufferedDensities.Add(line.ToString());
    }


    public void LogRunSummary()
    {
        if (summaryWriter == null)
        {
            Debug.LogError("Summary writer is not initialized.");
            return;
        }

        float[] alightingTime = new float[2];
        for(int i = 0; i < 2; i++)
        {
            alightingTime[i] = alightingEndTime[i] - alightingStartTime[i];
        }
        float[] boardingTime = new float[2];
        for(int i = 0; i < 2; i++)
        {
            boardingTime[i] = boardingEndTime[i] - boardingStartTime[i];
        }
        float[] totalTime = new float[2];
        for(int i = 0; i < 2; i++)
        {
            totalTime[i] = Mathf.Max(alightingEndTime[i], boardingEndTime[i]) - alightingStartTime[i];
        }
        float[] allAlightersExitTime = new float[2];
        for(int i = 0; i < 2; i++)
        {
            allAlightersExitTime[i] = allAlightersExitedTimestamp[i] - alightingStartTime[i];
        }

        float[,] avgTravelTime = new float[2, 2];
        float[,] avgDistance = new float[2, 2];
        float[,] avgSpeed = new float[2, 2];
        float[,] avgPathEfficiency = new float[2, 2];
        float[,] avgEntityDensity = new float[2, 2];
        float[,] avgSocialProximity = new float[2, 2];
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                avgTravelTime[i, j] = totalAgents[i, j] > 0 ? totalTravelTime[i, j] / totalAgents[i, j] : 0f;
                avgDistance[i, j] = totalAgents[i, j] > 0 ? totalDistance[i, j] / totalAgents[i, j] : 0f;
                avgSpeed[i, j] = totalAgents[i, j] > 0 ? totalSpeed[i, j] / totalAgents[i, j] : 0f;
                avgPathEfficiency[i, j] = totalAgents[i, j] > 0 ? totalPathEfficiency[i, j] / totalAgents[i, j] : 0f;
                avgEntityDensity[i, j] = totalAgents[i, j] > 0 ? totalEntityDensity[i, j] / totalAgents[i, j] : 0f;
                avgSocialProximity[i, j] = totalAgents[i, j] > 0 ? totalSocialProximity[i, j] / totalAgents[i, j] : 0f;
            }
        }

        int totalAgentsBoarding = totalAgents[0, 0] + totalAgents[0, 1];
        int totalAgentsAlighting = totalAgents[1, 0] + totalAgents[1, 1];

        float timeClearPlatformTotal = Mathf.Max(allAlightersExitedTimestamp[0], boardingEndTime[0], allAlightersExitedTimestamp[1], boardingEndTime[1]) - Mathf.Min(alightingStartTime[0], boardingStartTime[0], alightingStartTime[1], boardingStartTime[1]);
        float avgTravelTimeBoarding = totalAgentsBoarding > 0 ? (totalTravelTime[0, 0] + totalTravelTime[0, 1]) / totalAgentsBoarding : 0f;
        float avgTravelTimeAlighting = totalAgentsAlighting > 0 ? (totalTravelTime[1, 0] + totalTravelTime[1, 1]) / totalAgentsAlighting : 0f;
        float avgDistanceBoarding = totalAgentsBoarding > 0 ? (totalDistance[0, 0] + totalDistance[0, 1]) / totalAgentsBoarding : 0f;
        float avgDistanceAlighting = totalAgentsAlighting > 0 ? (totalDistance[1, 0] + totalDistance[1, 1]) / totalAgentsAlighting : 0f;
        float avgSpeedBoarding = totalAgentsBoarding > 0 ? (totalSpeed[0, 0] + totalSpeed[0, 1]) / totalAgentsBoarding : 0f;
        float avgSpeedAlighting = totalAgentsAlighting > 0 ? (totalSpeed[1, 0] + totalSpeed[1, 1]) / totalAgentsAlighting : 0f;
        float avgPathEfficiencyBoarding = totalAgentsBoarding > 0 ? (totalPathEfficiency[0, 0] + totalPathEfficiency[0, 1]) / totalAgentsBoarding : 0f;
        float avgPathEfficiencyAlighting = totalAgentsAlighting > 0 ? (totalPathEfficiency[1, 0] + totalPathEfficiency[1, 1]) / totalAgentsAlighting : 0f;

        float avgEntityDensityBoarding = totalAgentsBoarding > 0 ? (totalEntityDensity[0, 0] + totalEntityDensity[0, 1]) / totalAgentsBoarding : 0f;
        float avgEntityDensityAlighting = totalAgentsAlighting > 0 ? (totalEntityDensity[1, 0] + totalEntityDensity[1, 1]) / totalAgentsAlighting : 0f;
        float avgSocialProximityBoarding = totalAgentsBoarding > 0 ? (totalSocialProximity[0, 0] + totalSocialProximity[0, 1]) / totalAgentsBoarding : 0f;
        float avgSocialProximityAlighting = totalAgentsAlighting > 0 ? (totalSocialProximity[1, 0] + totalSocialProximity[1, 1]) / totalAgentsAlighting : 0f;


        StringBuilder line = new StringBuilder();
        line.Append(scenarioPrefix + ",");

        //Line 1
        line.AppendFormat(
            CultureInfo.InvariantCulture, 
            "{0:F2},{1:F2},{2:F2},{3:F2},", 
            alightingTime[0], 
            boardingTime[0], 
            totalTime[0], 
            allAlightersExitTime[0]
        );
        line.AppendFormat(
            CultureInfo.InvariantCulture, 
            "{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},{12},{13},", 
            avgTravelTime[0, 0], 
            avgTravelTime[1, 0], 
            avgDistance[0, 0], 
            avgDistance[1, 0],
            avgSpeed[0, 0],
            avgSpeed[1, 0],
            avgPathEfficiency[0, 0],
            avgPathEfficiency[1, 0],
            avgEntityDensity[0, 0],
            avgEntityDensity[1, 0],
            avgSocialProximity[0, 0],
            avgSocialProximity[1, 0],
            totalAgents[0, 0],
            totalAgents[1, 0]
        );

        // Line 2
        
        line.AppendFormat(
            CultureInfo.InvariantCulture, 
            "{0:F2},{1:F2},{2:F2},{3:F2},", 
            alightingTime[1], 
            boardingTime[1], 
            totalTime[1], 
            allAlightersExitTime[1]
        );
        line.AppendFormat(
            CultureInfo.InvariantCulture, 
            "{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},{12},{13},", 
            avgTravelTime[0, 1], 
            avgTravelTime[1, 1], 
            avgDistance[0, 1], 
            avgDistance[1, 1],
            avgSpeed[0, 1],
            avgSpeed[1, 1],
            avgPathEfficiency[0, 1],
            avgPathEfficiency[1, 1],
            avgEntityDensity[0, 1],
            avgEntityDensity[1, 1],
            avgSocialProximity[0, 1],
            avgSocialProximity[1, 1],
            totalAgents[0, 1],
            totalAgents[1, 1]
        );

        // Run-wide global metrics

        line.AppendFormat(
            CultureInfo.InvariantCulture, 
            "{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},{12:F2},{13},{14},{15}", 
            timeClearPlatformTotal,
            avgTravelTimeBoarding,
            avgTravelTimeAlighting,
            avgDistanceBoarding,
            avgDistanceAlighting,
            avgSpeedBoarding,
            avgSpeedAlighting,
            avgPathEfficiencyBoarding,
            avgPathEfficiencyAlighting,
            avgEntityDensityBoarding,
            avgEntityDensityAlighting,
            avgSocialProximityBoarding,
            avgSocialProximityAlighting,
            totalAgentsBoarding,
            totalAgentsAlighting,
            nYellowLineOversteps
        );

        summaryWriter.WriteLine(line.ToString());
    }

    public void LogYellowLineAndDensity()
    {
        if (yellowLineWriter != null)
        {
            foreach (string logLine in bufferedYellowLines)
            {
                yellowLineWriter.WriteLine(logLine);
            }
            bufferedYellowLines.Clear();
        }

        if (densityTimeSeriesWriter != null)
        {
            foreach (string logLine in bufferedDensities)
            {
                densityTimeSeriesWriter.WriteLine(logLine);
            }
            bufferedDensities.Clear();
        }

        if (agentMetricsWriter != null)
        {
            foreach (string logLine in bufferedAgentMetrics)
            {
                agentMetricsWriter.WriteLine(logLine);
            }
            bufferedAgentMetrics.Clear();
        }
    }

    void OnApplicationQuit()
    {
        CloseAllWriters();
    }

    private void WriteHeaderAgentMetrics()
    {
        if (agentMetricsWriter == null) return;
        StringBuilder header = new StringBuilder();
        header.Append("RunID,Platform,Scenario,FlowType,EntryFlowTotal,ExitFlowTotal,AgentType,TrainLine,TravelTime,Distance,Speed,PathEfficiency,EntityDensity,SocialProximity,EntryTimeStamp,ExitTimeStamp");
        agentMetricsWriter.WriteLine(header.ToString());
    }

    public void LogAgentMetrics(
        TrainController.AgentType agentType,
        int trainLine,
        float travelTime,
        float distance,
        float speed,
        float pathEfficiency,
        float entityDensity,
        float socialProximity,
        float entryTimeStamp,
        float exitTimeStamp)
    {
        if (agentMetricsWriter == null) return;

        StringBuilder line = new StringBuilder();
        line.Append(string.Join(",",
            testController.runIndex.ToString(),
            trainController.platformType.ToString(),
            testController.scenario.ToString(),
            testController.flowType.ToString(),
            testController.entryFlow.ToString(),
            testController.exitFlow.ToString(),
            agentType.ToString(),
            trainLine.ToString()
        ));
        line.Append(",");
        line.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2}",
            travelTime,
            distance,
            speed,
            pathEfficiency,
            entityDensity,
            socialProximity,
            entryTimeStamp,
            exitTimeStamp
        );
        bufferedAgentMetrics.Add(line.ToString());
    }
}