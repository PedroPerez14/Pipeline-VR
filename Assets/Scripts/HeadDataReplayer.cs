using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;

public class HeadDataReplayer : MonoBehaviour
{
    private const int STARTINGLOGINDEX = 3;                     //The first 3 lines of a log are headers, data starts on the 4th row, indexing from 0 converts it into 3

    [SerializeField] private VideoController videoController;   //We need a reference to tell the component which videos to play and when
    [SerializeField] private Camera cameraToRotate;             //We'll also need the main camera (head) to rotate it, replicating the movements from the loaded log data

    [Header("Log selection options")]
    public TextAsset[] logsToLoad;                              //Choose the .csv logs that contain the scanpaths you want to recreate

    // CACHES HOLDING THE LOG INFO IN DIFFERENT FORMATS IN CASE WE NEED THEM LATER //
    private string[][] logsCache;                               //We will load the logs in memory to access them later, line by line (string format)
    private float[][] logTimestampCache;                        //Cache of all the timestamps from the loaded logs (float format, so we don't have to parse them every frame later)
    private Quaternion[][] logQuaternionCache;                  //Cache of all the rotations from the loaded logs (Quaternion format, so we don't blah blah blah)
    private Vector3[][] logEulerAnglesCache;                    // . . . (EulerAngles format, for completeness)
    private Vector3[][] logForwardCache;                        //Cache of forward vectors (Vector3 format)
    private Vector2[][] logUVCache;                             //Cache of UV texture coordinates (Vector2 format)
    // ---------------------------------------------------------------------------- //

    private int[] associatedVideoIDs;                           //Video ID will automatically be extracted from the log's file name
    private float[] startingTimestamps;                         //Video timestamps where the tracking started, in seconds
    private float[] timeToPlayEachVideo;                        //Length of the logging process, in seconds (calculated directly from the log file)

    private double replayStartingTimestamp;                     //The instant of time where we start our replay, in milliseconds
    private double currentReplayTimestamp;                      //Our replay time, with this we will determine which logged values will be applied into the camera's (head's) rotation
    private int currentLogOnReplay;                             //Which log are we currently processing and replaying on screen
    private int lastSampleIndex;                                //The log line that contains data from the last timestamp that has already occurred (if our time is 7 and we have lines with stamps 4,6.5 and 7.1, this value will point to the line containing the 6.5)
    private int nextSampleIndex;                                //The log line containing the closest timestamp to our replay time, having currentReplayTime < timestamp of that line

    private Coroutine coroutine = null;
    private bool enableReplay = false;
    private bool isReady = false;                               //Needed for synchronization at the start of execution(?) //TODO
    


    void OnApplicationQuit()
    {
        if(coroutine != null)
        {
            StopCoroutine(coroutine);
            coroutine = null;
        }
    }

    private IEnumerator replayControlCoroutine()
    {
        for(int i = 0; i < logsToLoad.Length; i++)
        {
            //We start our own timer to know what logged rotations to interpolate, according to the timestamps specified in the cached logs
            replayStartingTimestamp = (double)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
            lastSampleIndex = 0;        
            nextSampleIndex = 1;        //lastSampleIndex + 1
            currentLogOnReplay = i;     //which replay index are we processing, this will be read by update()
            //from now on, Update() will interpolate the camera's position, and will get the right values for lastSampleIndex and nextSampleIndex
            startReplay();
            yield return new WaitForSeconds(timeToPlayEachVideo[i]);
            stopReplay();
            yield return new WaitForSeconds(videoController.timeBetweenClips);
        }
        Application.Quit();
        UnityEditor.EditorApplication.isPlaying = false;
    }

    // Start is called before the first frame update
    IEnumerator Start()
    {
        logsCache = new string[logsToLoad.Length][];
        associatedVideoIDs = new int[logsToLoad.Length];
        startingTimestamps = new float[logsToLoad.Length];
        timeToPlayEachVideo = new float[logsToLoad.Length];
        for (int i = 0; i < logsToLoad.Length; i++)
        {
            TextAsset log = logsToLoad[i];

            //First we extract the video ID from its log name
            string logName = log.name;
            string aux = logName.Split('_')[2];
            associatedVideoIDs[i] = int.Parse(aux[aux.Length - 1] + "");

            //Next, we read its data and store it line by line to access it later
            if (File.Exists(UnityEditor.AssetDatabase.GetAssetPath(logsToLoad[i])))
            {
                logsCache[i] = System.IO.File.ReadAllText(UnityEditor.AssetDatabase.GetAssetPath(log)).Split('\n');
            }

            //Lastly, we get some info we need to play the videos correctly, such as when to start the video and how long to play it
            // we will pass this info to the VideoController when ordering it to play the video clips
            startingTimestamps[i] = getStartingTimestamp(i);
            timeToPlayEachVideo[i] = getPlaytime(i);
        }

        //Once we have stored in memory the log files, we process them to extract and store their info, to avoid having to parse it later every time we need it
        GenerateLogCaches();
        isReady = true;
        yield return new WaitUntil(() => videoController.IsReady());    //Wait for the video player to be fully initialized before telling it to play anything
    }

    public IEnumerator replayQueuedLogs()
    {
        videoController.playVideoQueue(associatedVideoIDs, startingTimestamps, timeToPlayEachVideo);
        coroutine = StartCoroutine(replayControlCoroutine());
        yield return coroutine;
    }

    private void GenerateTimeStampsCache()
    {
        var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        ci.NumberFormat.NumberDecimalSeparator = ".";
        logTimestampCache = new float[logsToLoad.Length][];
        for (int i = 0; i < logsToLoad.Length; i++)
        {
            logTimestampCache[i] = new float[logsCache[i].Length - (STARTINGLOGINDEX + 1)];     //Last line of a log will be empty so we don't count that one
            for(int j = STARTINGLOGINDEX; j < logsCache[i].Length - 1; j++)                     //Same here
            {
                logTimestampCache[i][j - STARTINGLOGINDEX] = float.Parse(logsCache[i][j].Split(';')[0], ci);
            }
        }
    }

    private void GenerateEulerAnglesCache()
    {
        var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        ci.NumberFormat.NumberDecimalSeparator = ".";
        logEulerAnglesCache = new Vector3[logsToLoad.Length][];
        for (int i = 0; i < logsToLoad.Length; i++)
        {
            logEulerAnglesCache[i] = new Vector3[logsCache[i].Length - (STARTINGLOGINDEX + 1)];         //Last line of a log will be empty so we don't count that one
            for (int j = STARTINGLOGINDEX; j < logsCache[i].Length - 1; j++)                        //Same here
            {
                string aux = logsCache[i][j].Split(';')[1].Replace("(", "").Replace(")", "");
                float x = float.Parse(aux.Split(',')[0], ci);
                float y = float.Parse(aux.Split(',')[1], ci);
                float z = float.Parse(aux.Split(',')[2], ci);
                Vector3 deg = new Vector3(x, y, z);
                logEulerAnglesCache[i][j - STARTINGLOGINDEX] = deg;
            }
        }
    }

    private void GenerateQuaternionsCache()
    {
        var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        ci.NumberFormat.NumberDecimalSeparator = ".";
        logQuaternionCache = new Quaternion[logsToLoad.Length][];
        for (int i = 0; i < logsToLoad.Length; i++)
        {
            logQuaternionCache[i] = new Quaternion[logsCache[i].Length - (STARTINGLOGINDEX + 1)];       //Last line of a log will be empty so we don't count that one
            for (int j = STARTINGLOGINDEX; j < logsCache[i].Length - 1; j++)                            //Same here
            {
                string aux = logsCache[i][j].Split(';')[2].Replace("(", "").Replace(")", "");
                float x = float.Parse(aux.Split(',')[0], ci);
                float y = float.Parse(aux.Split(',')[1], ci);
                float z = float.Parse(aux.Split(',')[2], ci);
                float w = float.Parse(aux.Split(',')[3], ci);
                Quaternion quat = new Quaternion(x, y, z, w);
                logQuaternionCache[i][j - STARTINGLOGINDEX] = quat;
            }
        }
    }
    private void GenerateForwardCache()
    {
        var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        ci.NumberFormat.NumberDecimalSeparator = ".";
        logForwardCache = new Vector3[logsToLoad.Length][];
        for (int i = 0; i < logsToLoad.Length; i++)
        {
            logForwardCache[i] = new Vector3[logsCache[i].Length - (STARTINGLOGINDEX + 1)];         //Last line of a log will be empty so we don't count that one
            for (int j = STARTINGLOGINDEX; j < logsCache[i].Length - 1; j++)                        //Same here
            {
                string aux = logsCache[i][j].Split(';')[3].Replace("(", "").Replace(")", "");
                float x = float.Parse(aux.Split(',')[0], ci);
                float y = float.Parse(aux.Split(',')[1], ci);
                float z = float.Parse(aux.Split(',')[2], ci);
                Vector3 fwd = new Vector3(x, y, z);
                logForwardCache[i][j - STARTINGLOGINDEX] = fwd;            
            }
        }
    }

    private void GenerateUVCache()
    {
        var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        ci.NumberFormat.NumberDecimalSeparator = ".";
        logUVCache = new Vector2[logsToLoad.Length][];
        for (int i = 0; i < logsToLoad.Length; i++)
        {
            logUVCache[i] = new Vector2[logsCache[i].Length - (STARTINGLOGINDEX + 1)];         //Last line of a log will be empty so we don't count that one
            for (int j = STARTINGLOGINDEX; j < logsCache[i].Length - 1; j++)                   //Same here
            {
                string aux = logsCache[i][j].Split(';')[4].Replace("(", "").Replace(")", "");
                float u = float.Parse(aux.Split(',')[0], ci);
                float v = float.Parse(aux.Split(',')[1], ci);
                Vector3 uv = new Vector2(u,v);
                logUVCache[i][j - STARTINGLOGINDEX] = uv;
            }
        }
    }


    //Most of this data is redundant and will probably be unused, but we'll load it in memory anyways, just in case
    private void GenerateLogCaches()
    {
        GenerateTimeStampsCache();
        GenerateEulerAnglesCache();
        GenerateQuaternionsCache();
        GenerateForwardCache();
        GenerateUVCache();
    }

    private float getStartingTimestamp(int logIndex)
    {
        var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        ci.NumberFormat.NumberDecimalSeparator = ".";
        float retVal = float.Parse(logsCache[logIndex][0].Split(';')[1], ci);
        return retVal;
    }

    private float getPlaytime(int logIndex)
    {
        var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        ci.NumberFormat.NumberDecimalSeparator = ".";
        float retVal = float.Parse(logsCache[logIndex][logsCache[logIndex].Length - 2].Split(';')[0], ci);    // .Length - 2 because - 1 is just an empty line after the final '\n' and before the EOF char
        return retVal;
    }

    public void startReplay()
    {
        enableReplay = true;
    }

    public void stopReplay()
    {
        enableReplay = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(enableReplay)
        {
            currentReplayTimestamp = ((double)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds - replayStartingTimestamp) / 1000.0;
            while (nextSampleIndex < logTimestampCache[currentLogOnReplay].Length-1 && (float)currentReplayTimestamp >= logTimestampCache[currentLogOnReplay][nextSampleIndex]) //If the elapsed time between frames > time between 2 log entries, we seek the correct entry to interpolate
            {
                lastSampleIndex++;
                nextSampleIndex++;
            }
            float t = (logTimestampCache[currentLogOnReplay][nextSampleIndex] - logTimestampCache[currentLogOnReplay][lastSampleIndex]) / ((float)currentReplayTimestamp - logTimestampCache[currentLogOnReplay][lastSampleIndex]);
            cameraToRotate.transform.rotation = Quaternion.Lerp(logQuaternionCache[currentLogOnReplay][lastSampleIndex], logQuaternionCache[currentLogOnReplay][nextSampleIndex], t);
        }
    }

    public bool IsReady()
    {
        return isReady;
    }
}
