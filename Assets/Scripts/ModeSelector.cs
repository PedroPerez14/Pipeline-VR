using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DesiredProgramBehavior {playClipsAndDontLogData, playClipsAndLogData, logBasedReplayMode, replayDebugMode};

public class ModeSelector : MonoBehaviour
{
    [Header("Components that will make everything go BRR")]
    public VideoController videoController;
    public HeadPositionLogger headPositionLogger;
    public HeadDataReplayer headDataReplayer;

    [Header("Select one behavior for the program")]
    public DesiredProgramBehavior desiredProgramBehavior;

    void OnApplicationQuit()
    {
        Debug.Log("BYE BYE!");
    }

    // The program will start running here
    IEnumerator Start()
    {
        yield return new WaitUntil(() => (videoController.IsReady() && headPositionLogger.IsReady() && headDataReplayer.IsReady())); //Wait until every necessary component is ready to work properly
        switch (desiredProgramBehavior)
        {
            case DesiredProgramBehavior.playClipsAndDontLogData:                //Just show the videos
                headPositionLogger.enableLogging = false;
                videoController.playVideosRandomly();
                break;
            case DesiredProgramBehavior.playClipsAndLogData:                    //Show videos and log info; use this when conducting experiments
                headPositionLogger.enableLogging = true;
                videoController.playVideosRandomly();
                break;
            case DesiredProgramBehavior.logBasedReplayMode:                     //Tell HeadDataReplayer which logs to load and the program will create a live replay of them
                headPositionLogger.enableLogging = false;
                headDataReplayer.replayQueuedLogs();
                break;
            case DesiredProgramBehavior.replayDebugMode:                        //Same as logBasedReplayMode, but logging is enabled in order to compare replay logs to the original ones
                headPositionLogger.enableLogging = true;
                headDataReplayer.replayQueuedLogs();
                break;
            default:            //shouldn't happen
                break;
        }
    }
}
