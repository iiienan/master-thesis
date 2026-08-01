using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent manager that drives all simulation runs automatically.
/// Place this on a GameObject in your first/launcher scene (or any scene).
/// It survives scene reloads via DontDestroyOnLoad.
/// </summary>
public class RunManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------
    public static RunManager Instance { get; private set; }
    public int BaseBatchNumber { get; private set; }

    // -------------------------------------------------------------------------
    // Run descriptor
    // -------------------------------------------------------------------------
    public struct SimRun
    {
        public string sceneName;
        public TestController.Scenario scenario;
        public TrainController.Flow flowType;
        public int entryFlow;
        public int exitFlow;
        public bool alightBeforeBoarding;
        public int repetitionIndex; // Track which repetition this run is

        public override string ToString()
        {
            return $"[{sceneName}] {scenario} {flowType} " +
                   $"entry={entryFlow} exit={exitFlow} " +
                   $"ABB={alightBeforeBoarding} (Rep={repetitionIndex + 1})";
        }
    }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private List<SimRun> runs = new List<SimRun>();
    public int CurrentRunIndex { get; private set; } = 0;
    public int TotalRuns => runs.Count;

    // -------------------------------------------------------------------------
    // Configuration — edit here if needed
    // -------------------------------------------------------------------------
    [Header("Run Repetitions")]
    [Tooltip("Number of times to repeat each configuration in the matrix.")]
    [Min(1)]
    public int repetitionsPerConfig = 1; // Set via Unity Inspector

    private static readonly string[] Scenes =
    {
        "CentralPlatform",
        "SidePlatform",
        "MixedPlatform"
    };

    private static readonly int[] FlowValues =
    {
        500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 5500, 6000
    };

    [Header("Delays & Intervals")]
    public float arriveInterval = 120f;
    public float arrivalDelay = 0f;
    public float boardingDelay = 0f;
    public float exitingDelay = 0.6f;


    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BaseBatchNumber = GetNextBatchNumber();

        BuildRunList();
        Debug.Log($"[RunManager] Initialized. {TotalRuns} runs queued (Config Matrix x {repetitionsPerConfig} repetitions).");
        Debug.Log("Logging to: " + Application.persistentDataPath);
        LoadCurrentScene();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Debug.LogWarning("[RunManager] Force-skipping current run via Q.");
            Logger logger = FindObjectOfType<Logger>();
            if (logger != null)
            {
                logger.LogWarning("Run " + (CurrentRunIndex + 1) + " skipped by user." +
                " Entering Agents 1: " + Main.instance.nEnteringAgents[0] + ", Entering Agents 2: " + Main.instance.nEnteringAgents[1] + 
                ", Exiting Agents 1: " + Main.instance.nExitingAgentsPerLine[0] + ", Exiting Agents 2: " + Main.instance.nExitingAgentsPerLine[1]
                + ", Number of agents in the scene: " + Main.instance.agentList.Count);
            }
            OnRunComplete(true);
        }
        if(Main.instance.simulationTime >= 500f)
        {
            Debug.LogWarning("[RunManager] Skipping current run due to time limit.");
            Logger logger = FindObjectOfType<Logger>();
            if (logger != null)
            {
                logger.LogWarning("Run " + (CurrentRunIndex + 1) + " skipped due to time limit." +
                " Entering Agents 1: " + Main.instance.nEnteringAgents[0] + ", Entering Agents 2: " + Main.instance.nEnteringAgents[1] + 
                ", Exiting Agents 1: " + Main.instance.nExitingAgentsPerLine[0] + ", Exiting Agents 2: " + Main.instance.nExitingAgentsPerLine[1]
                + ", Number of agents in the scene: " + Main.instance.agentList.Count);
            }
            OnRunComplete(true);
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            Debug.LogWarning("[RunManager] Force-skipping current run via W. (No rerun)");
            Logger logger = FindObjectOfType<Logger>();
            if (logger != null)
            {
                logger.LogWarning("Run " + (CurrentRunIndex + 1) + " skipped by user. (No rerun)" +
                " Entering Agents 1: " + Main.instance.nEnteringAgents[0] + ", Entering Agents 2: " + Main.instance.nEnteringAgents[1] + 
                ", Exiting Agents 1: " + Main.instance.nExitingAgentsPerLine[0] + ", Exiting Agents 2: " + Main.instance.nExitingAgentsPerLine[1]
                + ", Number of agents in the scene: " + Main.instance.agentList.Count);
            }
            OnRunComplete(false);
        }
        
    }

    // -------------------------------------------------------------------------
    // Build the full run matrix
    // -------------------------------------------------------------------------
    private void BuildRunList()
    {
        runs.Clear();

        for (int rep = 0; rep < repetitionsPerConfig; rep++)
        {
            foreach (int flow in FlowValues)
            {
                for (int scenarioIndex = 1; scenarioIndex <= 5; scenarioIndex++)
                {
                    foreach (string scene in Scenes)
                    {
                        bool abb = (scene == "CentralPlatform" || scene == "SidePlatform");

                        TestController.Scenario scenario = TestController.Scenario.Entry;
                        TrainController.Flow flowType = TrainController.Flow.Symmetric;
                        int entryFlow = flow;
                        int exitFlow = flow;

                        switch (scenarioIndex)
                        {
                            case 1: // Symmetric Entry
                                scenario = TestController.Scenario.Entry;
                                flowType = TrainController.Flow.Symmetric;
                                entryFlow = flow;
                                exitFlow = flow / 3;
                                break;
                            case 2: // Asymmetric Entry
                                scenario = TestController.Scenario.Entry;
                                flowType = TrainController.Flow.Asymmetric;
                                entryFlow = flow;
                                exitFlow = flow / 3;
                                break;
                            case 3: // Symmetric Exit
                                scenario = TestController.Scenario.Exit;
                                flowType = TrainController.Flow.Symmetric;
                                entryFlow = flow / 3;
                                exitFlow = flow;
                                break;
                            case 4: // Asymmetric Exit
                                scenario = TestController.Scenario.Exit;
                                flowType = TrainController.Flow.Asymmetric;
                                entryFlow = flow / 3;
                                exitFlow = flow;
                                break;
                            case 5: // Symmetric Entry + Exit
                                scenario = TestController.Scenario.EntryExit;
                                flowType = TrainController.Flow.Symmetric;
                                entryFlow = flow;
                                exitFlow = flow;
                                break;
                        }

                        runs.Add(new SimRun
                        {
                            sceneName            = scene,
                            scenario             = scenario,
                            flowType             = flowType,
                            entryFlow            = entryFlow,
                            exitFlow             = exitFlow,
                            alightBeforeBoarding = abb,
                            repetitionIndex      = rep
                        });
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by TestController.Awake() to receive this run's parameters.
    /// Call this BEFORE building the log file name.
    /// </summary>
    public void ApplyParamsToTestController(TestController tc)
    {
        if (CurrentRunIndex >= TotalRuns)
        {
            Debug.LogWarning("[RunManager] ApplyParams called but no runs left.");
            return;
        }

        SimRun run = runs[CurrentRunIndex];
        Debug.Log($"[RunManager] Run {CurrentRunIndex + 1}/{TotalRuns}: {run}");

        tc.scenario             = run.scenario;
        tc.flowType             = run.flowType;
        tc.entryFlow            = run.entryFlow;
        tc.exitFlow             = run.exitFlow;
        tc.alightBeforeBoarding = run.alightBeforeBoarding;
        tc.runIndex             = CurrentRunIndex % (TotalRuns / repetitionsPerConfig);
        tc.arriveInterval       = arriveInterval;
        tc.log                  = true;
        
        tc.repetitionIndex      = run.repetitionIndex;
    }

    public void ApplyParamsToTrainController(TrainController tc)
    {
        if (CurrentRunIndex >= TotalRuns)
        {
            Debug.LogWarning("[RunManager] ApplyParamsToTrainController called but no runs left.");
            return;
        }

        tc.arrivalDelay = arrivalDelay;
        tc.boardingDelay = boardingDelay;
        tc.exitingDelay = exitingDelay;
    }

    /// <summary>
    /// Call this when the simulation finishes to advance to the next run.
    /// </summary>
    public void OnRunComplete(bool rerun = false)
    {
        Debug.Log($"[RunManager] Run {CurrentRunIndex + 1}/{TotalRuns} complete (rerun: {rerun}).");

        // Flush and close log files before the scene is destroyed
        Logger logger = FindObjectOfType<Logger>();

        if (logger != null)
        {
            if (!rerun)
            {
                logger.LogRunSummary();
                logger.LogYellowLineAndDensity();
            }
            logger.CloseAllWriters();
        }

        if (!rerun)
        {
            CurrentRunIndex++;
        }

        if (CurrentRunIndex < TotalRuns)
        {
            LoadCurrentScene();
        }
        else
        {
            Debug.Log("[RunManager] All runs complete!");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------
    public static int GetNextBatchNumber()
    {
        int maxBatch = 0;
        string path = Application.persistentDataPath;
        try
        {
            if (Directory.Exists(path))
            {
                string[] dirs = Directory.GetDirectories(path, "realBatch*");
                foreach (string dir in dirs)
                {
                    string folderName = Path.GetFileName(dir);
                    if (folderName.StartsWith("realBatch") && int.TryParse(folderName.Substring(9), out int num))
                    {
                        if (num > maxBatch)
                        {
                            maxBatch = num;
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RunManager] Error scanning for realBatch folders: {e.Message}");
        }
        return maxBatch + 1;
    }

    private void LoadCurrentScene()
    {
        string sceneName = runs[CurrentRunIndex].sceneName;
        Debug.Log($"[RunManager] Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}