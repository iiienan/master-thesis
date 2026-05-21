using System.IO;
using System.Text;
using UnityEngine;

public class TestController : MonoBehaviour
{
    public enum Scenario
    {
        Entry,
        Exit,
        EntryExit
    }
    public Scenario scenario;
    public TrainController.Flow flowType;
    public int entryFlow = 300;
    public int exitFlow = 300;
    public float arriveInterval = 240f;
    public bool alightBeforeBoarding;
    public bool log = true;

    internal string logFileNames;
    internal string logFolder;
    private TrainController trainController;
    private Main main;
    private Logger logger;
    public bool waitOutsideTrain = false;
    internal int runIndex = 0;

    internal int[] entryFlowLines = new int[2];
    internal int[] exitFlowLines = new int[2];


    private void Awake()
    {
        RunManager.Instance?.ApplyParamsToTestController(this);
        trainController = FindObjectOfType<TrainController>();
        if(trainController == null)
        {
            Debug.LogError("TrainController not found in the scene.");
            return;
        }
        main = FindObjectOfType<Main>();
        if (main == null)
        {
            Debug.LogError("Main not found in the scene.");
        }

        SetFlows();
        SetTrainControllerParameters();
    }


    private void OnValidate()
    {
        if(trainController == null)
        {
            trainController = FindObjectOfType<TrainController>();
        }
        if (trainController != null)
        {
            SetTrainControllerParameters();
        }
    }

    internal void SetFlows()
    {
        if (scenario == Scenario.Entry)
        {
            if(flowType == TrainController.Flow.Symmetric)
            {
                entryFlowLines[0] = entryFlow / 2;
                entryFlowLines[1] = entryFlow / 2;
            }
            else
            {
                entryFlowLines[0] = (int)(entryFlow * (4f / 5f));
                entryFlowLines[1] = (int)(entryFlow * (1f / 5f));
            }
            exitFlowLines[0] = exitFlow / 2;
            exitFlowLines[1] = exitFlow / 2;
        } 
        else if(scenario == Scenario.Exit)
        {
            if(flowType == TrainController.Flow.Symmetric)
            {
                exitFlowLines[0] = exitFlow / 2;
                exitFlowLines[1] = exitFlow / 2;
            }
            else
            {
                exitFlowLines[0] = (int)(exitFlow * (4f / 5f));
                exitFlowLines[1] = (int)(exitFlow * (1f / 5f));
            }
            entryFlowLines[0] = entryFlow / 2;
            entryFlowLines[1] = entryFlow / 2;
        }
        else if(scenario == Scenario.EntryExit)
        {
            entryFlowLines[0] = entryFlow / 2;
            entryFlowLines[1] = entryFlow / 2;
            exitFlowLines[0] = exitFlow / 2;
            exitFlowLines[1] = exitFlow / 2;
        }
    }

    internal void SetTrainControllerParameters()
    {
        trainController.flow = flowType;
        trainController.nEnteringAgents = entryFlow;
        trainController.arriveInterval = arriveInterval;
        trainController.alightBeforeBoarding = alightBeforeBoarding;
        trainController.trains[0].GetComponent<Train>().numberOfAgents = exitFlowLines[0];
        trainController.trains[1].GetComponent<Train>().numberOfAgents = exitFlowLines[1];
        trainController.nAgentsToAlight[0] = exitFlowLines[0];
        trainController.nAgentsToAlight[1] = exitFlowLines[1];
        trainController.nAgentsToBoard[0] = entryFlowLines[0];
        trainController.nAgentsToBoard[1] = entryFlowLines[1];
    }




}
