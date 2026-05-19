using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent manager that drives all simulation runs automatically.
/// Place this on a GameObject in your first/launcher scene (or any scene).
/// It survives scene reloads via DontDestroyOnLoad.
///
/// Total runs: 5 scenarios x 2 alightBeforeBoarding x 10 flow values x 3 scenes = 300 runs.
///
/// SETUP:
///   1. Add this script to a GameObject (e.g. "RunManager") in your first scene.
///   2. In your TestController.Awake(), after setting up parameters, call:
///          RunManager.Instance?.ApplyParamsToTestController(this);
///      Do this BEFORE building the log file name.
///   3. When your simulation finishes, call:
///          RunManager.Instance?.OnRunComplete();
/// </summary>
public class RunManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------
    public static RunManager Instance { get; private set; }

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

        public override string ToString()
        {
            return $"[{sceneName}] {scenario} {flowType} " +
                   $"entry={entryFlow} exit={exitFlow} " +
                   $"ABB={alightBeforeBoarding}";
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
    private static readonly string[] Scenes =
    {
        "CentralPlatform",
        "SidePlatform",
        "MixedPlatform"
    };

    private static readonly int[] FlowValues =
    {
        500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000
    };

    private Logger logger;

    public float arriveInterval = 120f;
    public float arrivalDelay = 15f;
    public float boardingDelay = 1f;
    public float exitingDelay = 5f;
    private TrainController trainController;


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

        BuildRunList();
        Debug.Log($"[RunManager] Initialized. {TotalRuns} runs queued.");
        Debug.Log("Logging to: " + Application.persistentDataPath);
        LoadCurrentScene();

        logger = FindObjectOfType<Logger>();
        if (logger == null)        
        {
            Debug.LogError("Logger not found in the scene.");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            Debug.LogWarning("[RunManager] Force-skipping current run via F9.");
            logger.LogWarning("Run " + (CurrentRunIndex + 1) + " skipped by user.");
            OnRunComplete();
        }
    }

    // -------------------------------------------------------------------------
    // Build the full run matrix
    // -------------------------------------------------------------------------
    private void BuildRunList()
    {
        runs.Clear();

        foreach (string scene in Scenes)
        {
            foreach (bool abb in new[] { false, true })
            {
                if (scene == "MixedPlatform" && abb) continue;

                foreach (int flow in FlowValues)
                {
                    // --- 1. Symmetric Entry ---
                    // Tested flow: entryFlow. exitFlow = entryFlow / 3.
                    runs.Add(new SimRun
                    {
                        sceneName          = scene,
                        scenario           = TestController.Scenario.Entry,
                        flowType           = TrainController.Flow.Symmetric,
                        entryFlow          = flow,
                        exitFlow           = flow / 3,
                        alightBeforeBoarding = abb
                    });

                    // --- 2. Asymmetric Entry ---
                    // Tested flow: entryFlow. exitFlow = entryFlow / 3.
                    runs.Add(new SimRun
                    {
                        sceneName          = scene,
                        scenario           = TestController.Scenario.Entry,
                        flowType           = TrainController.Flow.Asymmetric,
                        entryFlow          = flow,
                        exitFlow           = flow / 3,
                        alightBeforeBoarding = abb
                    });

                    // --- 3. Symmetric Exit ---
                    // Tested flow: exitFlow. entryFlow = exitFlow / 3.
                    runs.Add(new SimRun
                    {
                        sceneName          = scene,
                        scenario           = TestController.Scenario.Exit,
                        flowType           = TrainController.Flow.Symmetric,
                        entryFlow          = flow / 3,
                        exitFlow           = flow,
                        alightBeforeBoarding = abb
                    });

                    // --- 4. Asymmetric Exit ---
                    // Tested flow: exitFlow. entryFlow = exitFlow / 3.
                    runs.Add(new SimRun
                    {
                        sceneName          = scene,
                        scenario           = TestController.Scenario.Exit,
                        flowType           = TrainController.Flow.Asymmetric,
                        entryFlow          = flow / 3,
                        exitFlow           = flow,
                        alightBeforeBoarding = abb
                    });

                    // --- 5. Symmetric Entry + Exit ---
                    runs.Add(new SimRun
                    {
                        sceneName            = scene,
                        scenario             = TestController.Scenario.EntryExit,
                        flowType             = TrainController.Flow.Symmetric,
                        entryFlow            = flow / 2,
                        exitFlow             = flow / 2,
                        alightBeforeBoarding = abb
                    });
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

        tc.scenario            = run.scenario;
        tc.flowType            = run.flowType;
        tc.entryFlow           = run.entryFlow;
        tc.exitFlow            = run.exitFlow;
        tc.alightBeforeBoarding = run.alightBeforeBoarding;
        tc.runIndex            = CurrentRunIndex;
        tc.arriveInterval       = arriveInterval;
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
    public void OnRunComplete()
    {
        Debug.Log($"[RunManager] Run {CurrentRunIndex + 1}/{TotalRuns} complete.");

        // Flush and close log files before the scene is destroyed
        Logger logger = FindObjectOfType<Logger>();

        logger.LogRunSummary();

        if (logger != null)
            logger.CloseAllWriters();

        CurrentRunIndex++;

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
    private void LoadCurrentScene()
    {
        string sceneName = runs[CurrentRunIndex].sceneName;
        Debug.Log($"[RunManager] Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}