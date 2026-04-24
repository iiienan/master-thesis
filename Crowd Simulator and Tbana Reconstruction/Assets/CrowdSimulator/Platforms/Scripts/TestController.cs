using System.Text;
using UnityEngine;

public class TestController : MonoBehaviour
{
    public enum Scenario
    {
        Entry,
        Exit
    }
    public Scenario scenario;
    public TrainController.Flow flowType;
    public int entryFlow = 300;
    public int exitFlow = 300;
    public float arriveInterval = 240f;
    public bool alightBeforeBoarding;
    public bool log = true;

    internal string logFileNames;
    private TrainController trainController;
    private Main main;
    private Logger logger;
    public bool waitOutsideTrain = false;

    private void Awake()
    {
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
        if(log) logger = FindObjectOfType<Logger>();
        if (logger == null && log)
        {
            Debug.LogError("Logger not found in the scene.");
        }

        StringBuilder sb = new StringBuilder();

        sb.Append(trainController.platformType.ToString());
        sb.Append(scenario.ToString());
        sb.Append(flowType.ToString());

        if (scenario == Scenario.Entry)
        {
            sb.Append(entryFlow.ToString());
        }
        else if (scenario == Scenario.Exit)
        {
            sb.Append(exitFlow.ToString());
        }
        else
        {
            Debug.LogError("Unsupported scenario: " + scenario);
        }

        if (alightBeforeBoarding)
        {
            sb.Append("AB");
        }

        sb.Append(".csv");

        logFileNames = sb.ToString();

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

    internal void SetTrainControllerParameters()
    {
        trainController.flow = flowType;
        trainController.nAgents = entryFlow;
        trainController.arriveInterval = arriveInterval;
        trainController.alightBeforeBoarding = alightBeforeBoarding;
        Train train0 = trainController.trains[0].GetComponent<Train>();
        Train train1 = trainController.trains[1].GetComponent<Train>();

        if (flowType == TrainController.Flow.Asymmetric && scenario == Scenario.Exit)
        {
            train0.numberOfAgents = (int)(exitFlow * (4f / 5f));
            train1.numberOfAgents = (int)(exitFlow * (1f / 5f));
        }
        else
        {
            train0.numberOfAgents = exitFlow / 2;
            train1.numberOfAgents = exitFlow / 2;
        }
        trainController.waitOutsideTrain = waitOutsideTrain;
    }


    internal string BuildLogFileName(string prefix)
    {
        return prefix + logFileNames;
    }

}
