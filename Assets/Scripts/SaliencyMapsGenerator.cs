using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using OpenCvSharp;
using System.Threading.Tasks;
using System.Linq;

public enum FixationDetecionAlgorithm { VelocityTresholdIdentification };        //I-DT and I-VT for now

public class SaliencyMapsGenerator : MonoBehaviour
{
    /*
     * This script should receive one or more logs corresponding to the same video clip, and it will output the average saliency maps
     * after the n first seconds, n being <secondsToConsider>. This component will assume that all the logs are covering the total length of the video clips (//TODO change?)
     * or the same time window within it
     */

    struct Fixation
    {
        public float x;                                         //x position of the fixation, NORMALIZED in range 0..1 (u)
        public float y;                                         //y position of the fixation, NORMALIZED in range 0..1 (v)
        public float t;                                         //Starting time of the fixation
        public float d;                                         //Duration of the fixation
    }

    private const int STARTINGLOGINDEX = 3;                     //The first 3 lines of a log are headers, data starts on the 4th row, indexing from 0 converts it into 3

    [Header("Main config")]
    public TextAsset videoTitlesList;                           //This should be a .txt file generated at the same time as the logs that we are going to process //TODO necesario aquí?
    public TextAsset[] logsToProcess;                           //We'll validate that the video clip we're referring to from the log's name
    public VideoController videoController;                     //We'll need access to it in order to get the video frame starting and ending timestamps
    public float start = 0.0f;                                  //Timestamp of the video clip which saliency we want to compute for a certain amount of time
    public float secondsToConsider;                             //Time amount to compute saliency maps for, starting at the <start> timestamp. If it's longer than the log duration, this will be ignored
    [Header("If 0, get auto output size from clip")]
    public int outputFrameWidth = 0;                            //If set to 0, the program will use the video's frame width
    public int outputFrameHeight = 0;                           //If set to 0, the program will use the video's frame height

    [Header("Choose the desired output")]
    //---------- We'll let the users choose which maps they want to output --------//
    //public bool outputFrame = true;                           //Add this when i enable saliency map calculation for each frame
    public bool outputFixationMaps = true;
    public bool outputSaliencyMaps = true;
    //-----------------------------------------------------------------------------//

    [Header("Advanced config options")]
    public FixationDetecionAlgorithm fixationDetecionAlgorithm; //We can now choose between I-DT and V-DT
    public bool dynamicSpeedThreshold;                          //If true, IVT will discard top X% speeds and then set the speed threshold for fixations on 20% of the resulting max value
    [HideInInspector] public float dynamicThreshDiscardPercent; //The X% to discard
    [HideInInspector] public float speedThresholdIVT;           //Speed threshold (deg/s) to differentiate between a fixation and a saccade. (80-100 deg/sec should work)
    public int sigmaInDegs = 1;                                 //Expressed in degrees
    public int maxFixationsPerCentroid = 1;                     /*When we detect several fixations in consecutive samples, we calculate their average position.
                                                                  Sometimes it does not work well, so we limit the max amount of fixations.*/

    // CACHES HOLDING THE LOG INFO IN DIFFERENT FORMATS IN CASE WE NEED THEM LATER //
    private string[][] logsCache;                               //We will load the logs in memory to access them later, line by line (string format)
    private float[][] logTimestampCache;                        //Cache of all the timestamps from the loaded logs (float format, so we don't have to parse them every frame later)
    private Quaternion[][] logQuaternionCache;                  //Cache of all the rotations from the loaded logs (Quaternion format, so we don't blah blah blah)
    private Vector3[][] logEulerAnglesCache;                    // . . . (EulerAngles format, for completeness)
    private Vector3[][] logForwardCache;                        //Cache of forward vectors (Vector3 format)
    private Vector2[][] logUVCache;                             //Cache of UV texture coordinates (Vector2 format)
    private double[][] angularSpeeds;                           //We will need to convert rotations and timestamps to speeds in order to identify fixations and saccades
    // ---------------------------------------------------------------------------- //

    private Texture2D fixationMap;
    private bool isReady = false;                               //Needed for synchronization at the start of program execution (//TODO?)
    private bool abort = false;                                 //Start will check every parameter for inconsistencies, if it's set to true, this component won't calculate nor output anything
    private int clipIndex;                                      //To be used when naming the ouputted saliency/fixation maps as images
    private ulong startingFrame = 0;
    private ulong endingFrame = 0;
    private string clipTitle = "unknown";
    private string fixMapsPath;
    private string salMapsPath;

    // ------------------------------ OUTPUT FOLDERS ------------------------------ //
    private string outputsFolderName = "Outputs";
    private string fixMapsFolderName = "Fixations";
    private string salMapsFolderName = "Saliency";
    // ---------------------------------------------------------------------------- //


    private void GenerateLogCaches()
    {
        GenerateTimeStampsCache();
        GenerateEulerAnglesCache();
        GenerateQuaternionsCache();
        GenerateForwardCache();
        GenerateUVCache();
    }

    private void GenerateTimeStampsCache()
    {
        var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        ci.NumberFormat.NumberDecimalSeparator = ".";
        logTimestampCache = new float[logsToProcess.Length][];
        for (int i = 0; i < logsToProcess.Length; i++)
        {
            logTimestampCache[i] = new float[logsCache[i].Length - (STARTINGLOGINDEX + 1)];     //Last line of a log will be empty so we don't count that one
            for (int j = STARTINGLOGINDEX; j < logsCache[i].Length - 1; j++)                     //Same here
            {
                logTimestampCache[i][j - STARTINGLOGINDEX] = float.Parse(logsCache[i][j].Split(';')[0], ci);
            }
        }
    }
    private void GenerateEulerAnglesCache()
    {
        var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        ci.NumberFormat.NumberDecimalSeparator = ".";
        logEulerAnglesCache = new Vector3[logsToProcess.Length][];
        for (int i = 0; i < logsToProcess.Length; i++)
        {
            logEulerAnglesCache[i] = new Vector3[logsCache[i].Length - (STARTINGLOGINDEX + 1)];         //Last line of a log will be empty so we don't count that one
            for (int j = STARTINGLOGINDEX; j < logsCache[i].Length - 1; j++)                            //Same here
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
        logQuaternionCache = new Quaternion[logsToProcess.Length][];
        for (int i = 0; i < logsToProcess.Length; i++)
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
        logForwardCache = new Vector3[logsToProcess.Length][];
        for (int i = 0; i < logsToProcess.Length; i++)
        {
            logForwardCache[i] = new Vector3[logsCache[i].Length - (STARTINGLOGINDEX + 1)];             //Last line of a log will be empty so we don't count that one
            for (int j = STARTINGLOGINDEX; j < logsCache[i].Length - 1; j++)                            //Same here
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
        logUVCache = new Vector2[logsToProcess.Length][];
        for (int i = 0; i < logsToProcess.Length; i++)
        {
            logUVCache[i] = new Vector2[logsCache[i].Length - (STARTINGLOGINDEX + 1)];         //Last line of a log will be empty so we don't count that one
            for (int j = STARTINGLOGINDEX; j < logsCache[i].Length - 1; j++)                   //Same here
            {
                string aux = logsCache[i][j].Split(';')[4].Replace("(", "").Replace(")", "");
                float u = float.Parse(aux.Split(',')[0], ci);
                float v = float.Parse(aux.Split(',')[1], ci);
                Vector3 uv = new Vector2(u, v);
                logUVCache[i][j - STARTINGLOGINDEX] = uv;
            }
        }
    }

    private double HaversineOrthodormicDistance(double lat_1, double lat_2, double long_1, double long_2)
    {
        double deltaLong = long_2 - long_1;
        double deltaLat = lat_2 - lat_1;
        double retVal = 2.0 * Math.Asin(Math.Pow(Math.Sin(deltaLong / 2.0), 2.0) + Math.Cos(long_1) * Math.Cos(long_2) * Math.Pow(Math.Sin(deltaLat / 2.0), 2.0));
        return retVal;
    }

    public IEnumerator ComputeFixationsAndSalMaps()
    {
        if(abort)                                                                   //If config error detected during Start() method, it will stop here
        {
            yield break;
        }

        List<Fixation> fixations;
        switch (fixationDetecionAlgorithm)
        {
            case FixationDetecionAlgorithm.VelocityTresholdIdentification:
                GetAngularSpeeds();
                fixations = IVT();
                break;
            default:
                fixations = new List<Fixation>();
                break;
        }

        string fileDate = System.DateTime.UtcNow.ToLocalTime().ToString("ddMMyyyy_HHmmss");
        Directory.CreateDirectory(Application.dataPath + "/" + outputsFolderName + "/" + clipTitle.Replace(" ", "") + "_" + fileDate + "_" + logsToProcess.Length + "_users");
        if (outputSaliencyMaps)
        {
            salMapsPath = Application.dataPath + "/" + outputsFolderName + "/" + clipTitle.Replace(" ", "") + "_" + fileDate + "_" + logsToProcess.Length + "_users" + "/" + salMapsFolderName;
            Directory.CreateDirectory(salMapsPath);

        }
        if (outputFixationMaps)
        {
            fixMapsPath = Application.dataPath + "/" + outputsFolderName + "/" + clipTitle.Replace(" ", "") + "_" + fileDate + "_" + logsToProcess.Length + "_users" + "/" + fixMapsFolderName;
            Directory.CreateDirectory(fixMapsPath);
        }

        float[][] gaze_count;
        for (int currFrame = (int)startingFrame; currFrame <= (int)endingFrame; currFrame++)
        {
            fixationMap = new Texture2D(outputFrameWidth, outputFrameHeight, TextureFormat.RGB24, false);

            gaze_count = new float[outputFrameWidth][];
            for (int i = 0; i < outputFrameWidth; i++)
            {
                gaze_count[i] = new float[outputFrameHeight];
            }

            for (int i = 0; i < fixations.Count; i++)
            {
                Fixation fx = fixations[i];

                //We consider fixations that start within the selected frame, along with the ones that have started in a previous frame but will finish in this or other future frame(s)
                if((fx.t >= (float)currFrame / (float)videoController.videos[clipIndex].frameRate && fx.t <= ((float)currFrame + 1.0f) / (float)videoController.videos[clipIndex].frameRate) 
                    || (fx.t <= (float)currFrame / (float)videoController.videos[clipIndex].frameRate && fx.t + fx.d >= ((float)currFrame) / (float)videoController.videos[clipIndex].frameRate))
                {
                    int w = ((int)Math.Round(fx.x * outputFrameWidth, 0)) % (outputFrameWidth);
                    int h = ((int)Math.Round(fx.y * outputFrameHeight, 0)) % (outputFrameHeight);
                    if (w < 0)
                    {
                        Debug.Log("Fixation map width index error! " + w + " " + fx.x + " " + i);
                    }
                    if (h < 0)
                    {
                        Debug.Log("Fixation map height index error! " + h + " " + fx.y + " " + i);
                    }
                    gaze_count[w][h] += (1.0f / logsToProcess.Length);
                }
            }

            if (outputFixationMaps)                                                                                                              //Output it to a .PNG file if requested by the users
            {
                Color col;
                int circleradius = Mathf.RoundToInt(outputFrameWidth / (360.0f * 2.0f));
                for (int i = 0; i < outputFrameWidth; i++)
                {
                    for (int j = 0; j < outputFrameHeight; j++)
                    {
                        fixationMap.SetPixel(i, j, new Color(0, 0, 0));
                    }
                }

                for (int i = 0; i < outputFrameWidth; i++)
                {
                    for (int j = 0; j < outputFrameHeight; j++)
                    {
                        col = new Color(gaze_count[i][j], gaze_count[i][j], gaze_count[i][j]);
                        if(gaze_count[i][j] != 0.0f)
                        {
                            for (int l = circleradius * -1; l <= circleradius; l++)
                            {
                                for (int k = circleradius * -1; k <= circleradius; k++)
                                {
                                    if (!(Math.Abs(l) == circleradius && Math.Abs(k) == circleradius) 
                                        && i+l >= 0 && i+l < outputFrameWidth && ((outputFrameHeight - 1) - j) + k >= 0 
                                        && ((outputFrameHeight - 1) - j) + k < outputFrameHeight)                                               //Check texture limits
                                    {
                                        fixationMap.SetPixel(i + l, ((outputFrameHeight - 1) - j) + k, col);
                                    }
                                }
                            }
                        } 
                    }
                }
                fixationMap.Apply();                                                                                            //We're gonna need a Texture2D instead of the formerly created array
                var pngFixMap = fixationMap.EncodeToPNG();
                string dirPath = fixMapsPath + "/" + "FIX_startFrame_" + (int)startingFrame + "_currFrame_" + currFrame + "_of_" + (int)endingFrame + ".PNG";
                File.WriteAllBytes(dirPath, pngFixMap);
            }

            Mat fixMapMat = new Mat(fixationMap.height, fixationMap.width, MatType.CV_8UC3);
            Mat salMapMat = new Mat(fixationMap.height, fixationMap.width, MatType.CV_64F);

            Texture2DToMat(fixationMap, fixMapMat);

            float sigma_y = sigmaInDegs * ((float)salMapMat.Cols / 360.0f);
            int ksize = (int)(3 * sigma_y);
            if (ksize % 2 != 1)
            {
                ksize++;
            }
            salMapMat = fixMapMat;                                  //If i didn't do this, the saliency map would be initialized with memory trash data (lost 2 hours before realizing)
            for (int i = 0; i < salMapMat.Rows; i++)
            {
                Mat kernel_y = Cv2.GetGaussianKernel(ksize, sigma_y);
                float angle = Mathf.Abs(((float)(i+1) / (float)salMapMat.Rows) - 0.5f) * Mathf.PI;
                float sigma_x = sigma_y / (Mathf.Cos(angle));
                Mat kernel_x = Cv2.GetGaussianKernel(ksize, sigma_x);
                var row = salMapMat.Row[i];
                //Cv2.GaussianBlur(row, row, new Size(1, ksize), sigma_x, sigma_y);
                Cv2.SepFilter2D(row, row, -1, kernel_x, kernel_y);
                salMapMat.Row[i] = row;
            }

            salMapMat.Normalize();
            if (outputSaliencyMaps)
            {
                Cv2.ImWrite(salMapsPath + "/" + "SAL_startFrame_" + (int)startingFrame + "_currFrame_" + currFrame + "_of_" + (int)endingFrame + ".bmp", salMapMat);
            }
            Debug.Log("Calculating fixations and/or saliency: " + (((currFrame - (float)startingFrame) * 100.0f) / ((float)endingFrame - (float)startingFrame)) + "%");
            yield return null;
        }
    }

    //StackOverflow te quiero
    private void Texture2DToMat(Texture2D tex, Mat mat)
    {
        int width = tex.width;
        int height = tex.height;
        // Color32 array : r, g, b, a
        Color32[] c = tex.GetPixels32();
        Vec3b[] arrayData = new Vec3b[height * width];

        // Parallel for loop
        // convert Color32 object to Vec3b object
        // Vec3b is the representation of pixel for Mat
        
        for(int i = 0; i < tex.height; i++)
        {
            for (var j = 0; j < width; j++)
            {
                var col = c[j + i * width];
                var vec3 = new Vec3b
                {
                    Item0 = (byte)col.b,
                    Item1 = (byte)col.g,
                    Item2 = (byte)col.r
                };
                // set pixel to an array
                arrayData[j + (tex.height - i - 1) * width] = vec3;
            }
        }
        // assign the Vec3b array to Mat
        mat.SetArray(0, 0, arrayData);
    }
    private double[] RemoveZeros(double[] source)
    {
        return source.Where(i => i != 0.0).ToArray();
    }

    private double DynamicSpeedThresholdCalculation(double[] speeds)
    {
        Array.Sort(speeds);                                                                                             //Sort the speeds array
        int thrIdx = Mathf.RoundToInt((float)(speeds.Length) * (1.0f - (dynamicThreshDiscardPercent / 100.0f)));        //Get the index of the max speed, discarding the top X% (2% usually)
        return speeds[thrIdx - 1] * 0.2;                                                                                //[Kubler et al, 2015]
    }

    private List<Fixation> IVT()
    {
        List<Fixation> fixationsList = new List<Fixation>();
        double speedThreshold = (double)speedThresholdIVT;

        for(int i = 0; i < angularSpeeds.Length; i++)                                       //For every log we are using to calculate
        {
            if (dynamicSpeedThreshold)
            {
                speedThreshold = DynamicSpeedThresholdCalculation(angularSpeeds[i]);
                Debug.Log("El umbral de velodidad de una fijación es ahora de " + speedThreshold + " para el log " + i + " tras descartar el " + dynamicThreshDiscardPercent + "%");
            }

            for (int j = 0; j < angularSpeeds[i].Length; j++)                                                           //For n points in a scanpath we have n-1 speeds
            {
                if (logTimestampCache[i][j] >= start && logTimestampCache[i][j] <= (start + secondsToConsider))         //Check time window          
                {
                    Fixation fixation = new Fixation();
                    fixation.x = 0;
                    fixation.y = 0;
                    fixation.t = Mathf.Infinity;
                    fixation.d = 0;
                    int fixationsToCentroid = 0;

                    //A point can either be a fixation or a saccade, depending of its speed and the threshold we choose
                    while (j < angularSpeeds[i].Length && angularSpeeds[i][j] <= speedThreshold && fixationsToCentroid <= maxFixationsPerCentroid)
                    {
                        fixation.x += logUVCache[i][j].x;
                        fixation.y += logUVCache[i][j].y;
                        fixation.t = Mathf.Min(fixation.t, logTimestampCache[i][j]);
                        fixation.d = logTimestampCache[i][j + 1] - fixation.t;

                        fixationsToCentroid++;
                        j++;
                    }
                    fixation.x /= (float)fixationsToCentroid;
                    fixation.y /= (float)fixationsToCentroid;
                    if (fixationsToCentroid > 0)
                    {
                        fixationsList.Add(fixation);
                    }
                }
                //j--;          //OJO que creo que no hace falta pero ten cuidado si explota
            }
        }
        return fixationsList;
    }

    private void GetAngularSpeeds()
    {
        angularSpeeds = new double[logEulerAnglesCache.Length][];
        for (int i = 0; i < logEulerAnglesCache.Length; i++)
        {
            angularSpeeds[i] = new double[logEulerAnglesCache[i].Length - 1];
            for (int j = 1; j < logEulerAnglesCache[i].Length; j++)
            {
                //if(logTimestampCache[i][j - 1] >= start && logTimestampCache[i][j - 1] <= (start + secondsToConsider))
                //{
                double lat_1 = logEulerAnglesCache[i][j - 1].x;
                double lat_2 = logEulerAnglesCache[i][j].x;
                double long_1 = logEulerAnglesCache[i][j - 1].y;
                double long_2 = logEulerAnglesCache[i][j].y;

                angularSpeeds[i][j - 1] = (HaversineOrthodormicDistance(lat_1, lat_2, long_1, long_2) * 180.0 / Math.PI) / (double)(logTimestampCache[i][j] - logTimestampCache[i][j - 1]);
                //}
            }
        }
    }

    // Start is called before the first frame update.
    // We will store the logs info to use it later for our calculations
    void Start()
    {
        //We extract the associated video clip and title
        TextAsset log = logsToProcess[0];
        string logName = log.name;
        string aux = logName.Split('_')[2];
        clipIndex = int.Parse(aux[aux.Length - 1] + "");
        string[] titlesList;
        if (File.Exists(UnityEditor.AssetDatabase.GetAssetPath(videoTitlesList)))
        {
            titlesList = System.IO.File.ReadAllText(UnityEditor.AssetDatabase.GetAssetPath(videoTitlesList)).Split('\n');
            clipTitle = titlesList[clipIndex];
        }

        logsCache = new string[logsToProcess.Length][];
        for (int i = 0; i < logsToProcess.Length; i++)
        {
            //Next, we read its data and store it line by line to access it later
            if (File.Exists(UnityEditor.AssetDatabase.GetAssetPath(logsToProcess[i])))
            {
                logsCache[i] = System.IO.File.ReadAllText(UnityEditor.AssetDatabase.GetAssetPath(logsToProcess[i])).Split('\n');
            }
        }

        //Once we have stored in memory the log files, we process them to extract and store their info, to avoid having to parse it later every time we need it
        GenerateLogCaches();

        //Get the corresponding video frames and check if they are within the video clip's expected range
        startingFrame = (ulong)Math.Floor(videoController.videos[clipIndex].frameRate * start);
        endingFrame = (ulong)Math.Floor(videoController.videos[clipIndex].frameRate * (start + secondsToConsider));
        if (startingFrame < 0 || endingFrame < 0 || startingFrame >= videoController.videos[clipIndex].frameCount || endingFrame >= videoController.videos[clipIndex].frameCount)
        {
            Debug.Log("Error: Trying the desired time window isn't fully contained within the selected video clip frame count! Quitting...");
            abort = true;
        }
        Debug.Log("The desired time window corresponds to frames " + startingFrame + " to " + endingFrame + " on clip " + clipIndex + ": " + clipTitle);

        for(int i = 0; i < logTimestampCache.Length; i++)
        {
            if (start < logTimestampCache[i][0] || (start + secondsToConsider) >= logTimestampCache[i][logTimestampCache[i].Length - 1])
            {
                Debug.Log("Error: Trying to calculate a bigger time window than the one logged in log " + logsToProcess[i].name + 
                    "\nLogged time interval is [" + logTimestampCache[i][0] + " -- " + logTimestampCache[i][logTimestampCache[i].Length - 1] + 
                    "] and specified time interval is [" + start + " -- " + (start+secondsToConsider) + "]");
                abort = true;
            }
        }
        

        //Lastly, check if user has set the desired size for the outputted images, if 0 we have to get it from the video clip's frame dimensions
        if (outputFrameWidth == 0)
        {
            outputFrameWidth = (int)videoController.videos[clipIndex].width;
        }
        if(outputFrameHeight == 0)
        {
            outputFrameHeight = (int)videoController.videos[clipIndex].height;
        }

        isReady = true;
        //CalculateFixations();                   //Called from mode selector, which acts as an entry point to the program
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public bool IsReady()
    {
        return isReady;
    }
}
