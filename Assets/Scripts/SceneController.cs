/*
 * Author: Pedro José Pérez García, 756642
 * Date: 24-02-2022 (last revision)
 * Comms: Trabajo de fin de grado de Ingeniería Informática, Graphics and Imaging Lab, Universidad de Zaragoza
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PupilLabs;

public enum DesiredProgramBehavior {playClipsAndDontLogData, playClipsAndLogData, logBasedReplayMode, computeSaliencyMaps};    //Add more behavior modes here

public class SceneController : MonoBehaviour
{
    [Header("Components that will make everything work")]
    public VideoController videoController;
    public HeadPositionLogger headPositionLogger;
    public EyeDataLogger eyeDataLogger;
    public ScanpathReplayer scanpathReplayer;
    public SaliencyMapsGenerator saliencyMapsGenerator;
    [Header("Components that calibrate the eye tracker")]
    public GameObject calibrationRoom;
    public CalibrationController calibrationController;
    public RequestController requestController;
    public GameObject gazeTrackerComponents;

    [Header("Select one behavior for the program")]
    public DesiredProgramBehavior desiredProgramBehavior;

    private bool calSuccess;

    ///---------------------------------------------------------------------///

    void OnEnable()
    {
        calibrationController.OnCalibrationSucceeded += CalibrationSucceeded;
    }

    void OnDisable()
    {
        calibrationController.OnCalibrationSucceeded -= CalibrationSucceeded;
    }

    private void CalibrationSucceeded()
    {
        calSuccess = true;
    }

    void OnApplicationQuit()
    {
        Debug.Log("BYE BYE!");
    }

    IEnumerator WaitForCalibration()
    {
        Debug.Log("Waiting for calibration to begin");
        yield return new WaitUntil(() => calibrationController.IsCalibrating);
        Debug.Log("Waiting for calibration to end");
        yield return new WaitUntil(() => calSuccess);
        Debug.Log("Calibration success!");
    }

    // Entry point for the tool
    IEnumerator Start()
    {
        calSuccess = false;
        yield return new WaitUntil(() => (videoController.IsReady() && eyeDataLogger.IsReady() && headPositionLogger.IsReady() && scanpathReplayer.IsReady() && saliencyMapsGenerator.IsReady())); //Wait until every necessary component is ready to work properly
        switch (desiredProgramBehavior)
        {
            case DesiredProgramBehavior.playClipsAndDontLogData:                        //Just show the videos, no need for calibration
                saliencyMapsGenerator.enabled = false;
                headPositionLogger.enableLogging = false;
                eyeDataLogger.enableLogging = false;
                scanpathReplayer.disableGazeVisualizer();
                yield return StartCoroutine(videoController.playVideosRandomly());
                break;
            case DesiredProgramBehavior.playClipsAndLogData:                            //Show videos and log info; use this when conducting experiments
                saliencyMapsGenerator.enabled = false;
                headPositionLogger.enableLogging = true;
                eyeDataLogger.enableLogging = true;
                scanpathReplayer.disableGazeVisualizer();
                gazeTrackerComponents.SetActive(true);                                 //disable gaze tracking
                Debug.Log("Waiting for the HMD to connect...");
                yield return new WaitUntil(() => requestController.IsConnected);        //Wait until the VR glasses are connected
                calibrationRoom.SetActive(true);                                        //show the calibration room
                yield return StartCoroutine(WaitForCalibration());                      //Wait for the calibration to end
                calibrationRoom.SetActive(false);                                       //Hide the calibration room to show the videos
                yield return new WaitForSeconds(1);                                     //wait a bit before showing the videos
                yield return StartCoroutine(videoController.playVideosRandomly());
                break;
            case DesiredProgramBehavior.logBasedReplayMode:                             //Tell HeadDataReplayer which logs to load and the program will create a live replay of them (no need to callibrate)
                saliencyMapsGenerator.enabled = false;
                headPositionLogger.enableLogging = false;
                eyeDataLogger.enableLogging = false;
                yield return StartCoroutine(scanpathReplayer.replayQueuedLogs());
                break;
            case DesiredProgramBehavior.computeSaliencyMaps:                            //No need for calibration
                headPositionLogger.enableLogging = false;
                eyeDataLogger.enableLogging = false;
                scanpathReplayer.disableGazeVisualizer();
                yield return StartCoroutine(saliencyMapsGenerator.ComputeFixationsAndSalMaps());     //Should work ok (i guess)
                break;
            default:                                                                    //shouldn't happen
                break;
        }
        Application.Quit();
        UnityEditor.EditorApplication.isPlaying = false;
    }
}
