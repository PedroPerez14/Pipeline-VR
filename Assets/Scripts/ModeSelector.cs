using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DesiredProgramBehavior {playClipsAndDontLogData, playClipsAndLogData, logBasedReplayMode, replayDebugMode, computeSaliencyMaps};

public class ModeSelector : MonoBehaviour
{
    [Header("Components that will make everything work")]
    public VideoController videoController;
    public HeadPositionLogger headPositionLogger;
    public HeadDataReplayer headDataReplayer;
    public SaliencyMapsGenerator saliencyMapsGenerator;

    [Header("Select one behavior for the program")]
    public DesiredProgramBehavior desiredProgramBehavior;

    void OnApplicationQuit()
    {
        Debug.Log("BYE BYE!");
    }

    // The program will start running here
    IEnumerator Start()
    {
        yield return new WaitUntil(() => (videoController.IsReady() && headPositionLogger.IsReady() && headDataReplayer.IsReady() && saliencyMapsGenerator.IsReady())); //Wait until every necessary component is ready to work properly
        switch (desiredProgramBehavior)
        {
            case DesiredProgramBehavior.playClipsAndDontLogData:                        //Just show the videos
                headPositionLogger.enableLogging = false;
                yield return StartCoroutine(videoController.playVideosRandomly());
                break;
            case DesiredProgramBehavior.playClipsAndLogData:                            //Show videos and log info; use this when conducting experiments
                headPositionLogger.enableLogging = true;
                yield return StartCoroutine(videoController.playVideosRandomly());
                break;
            case DesiredProgramBehavior.logBasedReplayMode:                             //Tell HeadDataReplayer which logs to load and the program will create a live replay of them
                headPositionLogger.enableLogging = false;
                yield return StartCoroutine(headDataReplayer.replayQueuedLogs());
                break;
            case DesiredProgramBehavior.replayDebugMode:                                //Same as logBasedReplayMode, but logging is enabled in order to compare replay logs to the original ones
                headPositionLogger.enableLogging = true;
                yield return StartCoroutine(headDataReplayer.replayQueuedLogs());
                break;
            case DesiredProgramBehavior.computeSaliencyMaps:
                headPositionLogger.enableLogging = false;
                yield return StartCoroutine(saliencyMapsGenerator.ComputeFixationsAndSalMaps());     //Work in progress, still testing it
                break;
            default:            //shouldn't happen
                break;
        }
        Application.Quit();
        UnityEditor.EditorApplication.isPlaying = false;
    }
}
