using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor.Rendering;
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

    private String logFileNames;
    private TrainController trainController;
    private Main main;
    private DensityLogger densityLogger;
    private Logger logger;

    private void Awake()
    {
        trainController = FindObjectOfType<TrainController>();
        main = FindObjectOfType<Main>();
        if(log) densityLogger = FindObjectOfType<DensityLogger>();
        if(log) logger = FindObjectOfType<Logger>();
        if (trainController == null)
        {
            Debug.LogError("TrainController not found in the scene.");
        }
        if (main == null)
        {
            Debug.LogError("Main not found in the scene.");
        }
        if (densityLogger == null && log)
        {
            Debug.LogError("DensityLogger not found in the scene.");
        }
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
        trainController = FindObjectOfType<TrainController>();
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

        if (flowType == TrainController.Flow.Asymmetric && scenario == Scenario.Exit)
        {
            trainController.trains[0].GetComponent<Train>().numberOfAgents = (int)(exitFlow * (4f / 5f));
            trainController.trains[1].GetComponent<Train>().numberOfAgents = (int)(exitFlow * (1f / 5f));
        }
        else
        {
            trainController.trains[0].GetComponent<Train>().numberOfAgents = exitFlow / 2;
            trainController.trains[1].GetComponent<Train>().numberOfAgents = exitFlow / 2;
        }
    }

    internal String SetDensityLogFileName()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Density");
        sb.Append(logFileNames);
        return sb.ToString();
    }

    internal String SetTravelTimeLogFileName()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("TravelTime");
        sb.Append(logFileNames);
        return sb.ToString();
    }

    internal String SetSimulationLogFileName()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Simulation");
        sb.Append(logFileNames);
        return sb.ToString();
    }

    internal String SetYellowLineLogFileName()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("YellowLine");
        sb.Append(logFileNames);
        return sb.ToString();
    }

    internal String SetTravelDistanceLogFileName()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("TravelDistance");
        sb.Append(logFileNames);
        return sb.ToString();
    }

}
