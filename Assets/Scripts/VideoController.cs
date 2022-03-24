/*
 * Author: Pedro José Pérez García, 756642
 * Date: 22-03-2022 (last revision)
 * Comms: Trabajo de fin de grado de Ingeniería Informática, Graphics and Imaging Lab, Universidad de Zaragoza
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]

public class VideoController : MonoBehaviour
{   
    [System.Serializable]
    public struct VideoData
    {
        public VideoClip video;
        public Video3DLayout layout;                        //0 means mono video, 1 = stereo side by side, 2 = stereo over under format
    }

    public enum StartingHeadOrientation { noRotation, randomize90DegIntervals, fullyRandomized };

    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject cameraParent;
    [Header("Video pool options")]
    [SerializeField] public VideoData[] videos;
    [SerializeField] public AudioClip[] audios;             //Please make sure to place the audio clips in the same position as their corresponding video clip
    public int numberOfVideosToShow;                        //Number of videos to be randomly shown to the user among the preloaded ones

    [Header("Logging config")]
    [SerializeField] private HeadPositionLogger headLogger;
    [SerializeField] private EyeDataLogger eyeDataLogger;

    [Header("Video showing config")]
    [SerializeField] public float timeToShowEachClip;       //Expressed in seconds
    [SerializeField] public bool findCubeBetweenClips;      //By enabling this a cube will be placed in the 3d space, looking at it will trigger the next video clip on the list //TODO
    [HideInInspector] public float timeBetweenClips;        //Expressed in seconds
    [SerializeField] public float audioVolume;              //This should affect the ambisonic audio clip. Affects both experiment and replay mode!   
    [SerializeField] public bool enableSound;               //This should affect the ambisonic audio, the video clips will ALWAYS be muted. Affects both experiment and replay mode!
    [SerializeField] public bool randomStartingTimeStamp;   //Start at 0:00 or at a random timestamp on the interval {0:00 .. (videoLength - timeToShowEachClip)}
    [SerializeField] public StartingHeadOrientation startingHeadOrientation;
    [HideInInspector] public GameObject cubeToTriggerPlay;  //If findCubeBetweenClips, looking at this cube for (0.2?) secs will play the next video and audio clips
    [SerializeField] public float timeLookingAtObjectBeforeTriggeringNextVideo = 0.2f;

    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private bool[] alreadyShownVideos;                      //To track which videos have already been shown to the user and avoid playing them more than once
    private Coroutine coroutine = null;
    private float lookAtObjectTimer;
    private bool objectHasBeenFound;                        //Has the user been staring at the 3d object (cube by default) during the required amount of time (~0.2s) before playing the next clip?

    private bool isReady = false;                           //Needed for synchronization at the start of execution


    void OnApplicationQuit()
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
            coroutine = null;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        videoPlayer = gameObject.GetComponent<VideoPlayer>();
        audioSource = gameObject.GetComponent<AudioSource>();

        videoPlayer.clip = null;
        audioSource.clip = null;
        videoPlayer.playOnAwake = false;                    //If something's broken in the future, check this
        audioSource.playOnAwake = false;
        alreadyShownVideos = new bool[videos.Length];

        if(numberOfVideosToShow > videos.Length)            //Additional check, just in case
        {
            numberOfVideosToShow = videos.Length;   
        }

        for(int i = 0; i < alreadyShownVideos.Length; i++)
        {
            alreadyShownVideos[i] = false;
        }

        objectHasBeenFound = false;
        lookAtObjectTimer = 0.0f;
        cubeToTriggerPlay.SetActive(false);                   //Hide the cube for now

        isReady = true;
    }

    /// Call one of the following two somewhere (i do it on ModeSelector, works as an entry point) ///
    public void playVideoQueue(int[] IDs, float[] startingTimestamps, float[] timeToPlayEach)
    {
        coroutine = StartCoroutine(VideoLoopFromQueue(IDs, startingTimestamps, timeToPlayEach));
    }

    public IEnumerator playVideosRandomly()
    {
        coroutine = StartCoroutine(VideoLoop());
        string[] videoNames = new string[videos.Length];
        string[] audioNames = new string[audios.Length];                                                //videos[] and audios[] should have the same length
        for(int i = 0; i < videos.Length; i++)
        {
            videoNames[i] = videos[i].video.name;
            audioNames[i] = audios[i].name;
        }
        headLogger.CreateVideoNamesList(videoNames);                                                    //Create a .txt file containing the order of the video clips right before the logging starts
        headLogger.CreateAudioNamesList(audioNames);                                                    //Create a .txt file containing the order of the audio clips right before the logging starts
        yield return coroutine;
    }

    private void PlaceObjectAroundUser()
    {
        cubeToTriggerPlay.transform.position = new Vector3(Random.Range(1.0f, 5.0f) * Mathf.Sign(Random.Range(-1.0f, 1.0f)), Random.Range(1.0f, 5.0f) * Mathf.Sign(Random.Range(-1.0f, 1.0f)), Random.Range(1.0f, 5.0f) * Mathf.Sign(Random.Range(-1.0f, 1.0f)));
    }

    private IEnumerator WaitForUserToFindObject()
    {
        PlaceObjectAroundUser();
        cubeToTriggerPlay.SetActive(true);                                                                  //Show the cube
        Debug.Log("Waiting for user to find cube before playing the next video clip...");
        yield return new WaitUntil(() => objectHasBeenFound);
        Debug.Log("Cube found! Playing next video...");
        lookAtObjectTimer = 0.0f;                                                                           //Reset time count
        objectHasBeenFound = false;                                                                         //And flag's value
        cubeToTriggerPlay.SetActive(false);                                                                 //Finally, hide the cube again
    }

    private IEnumerator VideoLoop()
    {
        for (int i = 0; i < numberOfVideosToShow; i++)
        {
            int videoID = ChooseRandomVideo();
            AdjustRenderTextureDimensions(videoID);                                                         //In case the video clips have different resolutions, we'll adjust the renderTexture
            PlayMedia(videoID);                                                                             //video.play() and audio.play() with extra steps to make sure everything's ok
            yield return new WaitForSeconds(Mathf.Min((float)videoPlayer.clip.length, timeToShowEachClip)); //Play the video clip for the specified amount of time or clip duration, if it's shorter than expected
            StopCurrentlyPlayingMedia(videoID);
            if (i != numberOfVideosToShow - 1)                                                              //No need to show the cube / wait time after the alst video clip
            {
                if (findCubeBetweenClips)
                {
                    yield return StartCoroutine(WaitForUserToFindObject());                                 //Users will have to look at this cube for at least 0.2 seconds, to make sure they are focusing their attention on it
                }
                else
                {
                    yield return new WaitForSeconds(timeBetweenClips);                                      //Delay between clips for a specified amount of time
                }
            }        
        }
        Application.Quit();
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private IEnumerator VideoLoopFromQueue(int[] IDs, float[] startingTimestamps, float[] timeToPlayEach)   //HeadDataReplayer will load a log queue and call this to play those clips
    {
        for(int i = 0; i < IDs.Length; i++)
        {
            AdjustRenderTextureDimensions(IDs[i]);
            PlayMediaFromTimestamp(IDs[i], startingTimestamps[i]);                      
            yield return new WaitForSeconds(timeToPlayEach[i]);                         //need a second float array from the logs extracted data to determine the playing time, and a third one for the starting timestamps
            StopCurrentlyPlayingMedia(IDs[i]);                                          
            yield return new WaitForSeconds(timeBetweenClips);
        }
        Application.Quit();
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private void AdjustRenderTextureDimensions(int videoID)
    {
        videoPlayer.targetTexture.Release();                                            //We cannot properly redimension the RenderTexture without relesaing it first
        videoPlayer.targetTexture.width = (int)videos[videoID].video.width;
        videoPlayer.targetTexture.height = (int)videos[videoID].video.height;
        RenderSettings.skybox.SetFloat("_Layout", (int)videos[videoID].layout);         //[Enum(None, 0, Side by Side, 1, Over Under, 2)] _Layout("3D Layout", Float) = 0 from the shader's code
    }

    private void StopCurrentlyPlayingMedia(int videoID)
    {
        if(headLogger.IsLogging())
        {
            headLogger.StopLogging();
        }

        if(eyeDataLogger.IsLogging())
        {
            eyeDataLogger.StopLogging();
        }
        videoPlayer.Stop();
        audioSource.Stop();
        videoPlayer.targetTexture.Release();
    }

    private float PrepareMediaToPlay(int id)
    {
        float startTime = 0.0f;                 //Starting video/audio timestamp                        
        videoPlayer.Prepare();                  //Required if we want to seek to a random timestamp before playing the video

        //Mute audio from the video clip, ambisonic audio will be loaded and played through a different file
        for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
        {
            videoPlayer.SetDirectAudioMute(i, true);
        }

        if(!enableSound)
        {
            audioVolume = 0.0f;
        }
        audioSource.volume = audioVolume;       //We control the AudioSource volume from this component, value in range [0..1]


        alreadyShownVideos[id] = true;
        //If true, video will start playing at a random moment in the interval [0..(ending - "timeToShowEachClip")].
        //  If the video is too short, it just plays fully from the beginning
        if (randomStartingTimeStamp)
        {
            if (videoPlayer.canSetTime)
            {
                if (videoPlayer.clip.length > timeToShowEachClip && audioSource.clip.length > timeToShowEachClip)
                {
                    startTime = Random.Range(0.0f, (float)(videoPlayer.clip.length - timeToShowEachClip));
                    videoPlayer.time = (double)startTime;
                    audioSource.time = startTime;
                    Debug.Log("Starting video reproduction at " + (double)startTime + " seconds " + videoPlayer.clip.length);  //DEBUG
                }
                else
                {
                    Debug.Log("Vídeo/audio muy corto, no se puede randomizar el timestamp de inicio!");
                }
            }
            else
            {
                Debug.Log("videoPlayer.canSetTime == false!");
            }
        }

        //Set playback speed after getting the clip prepared
        if (videoPlayer.canSetPlaybackSpeed)
        {
            videoPlayer.playbackSpeed = 1.0f;   //playbackSpeed;
            audioSource.pitch = 1.0f;           //playbackSpeed;                      //Pitch is directly linked to audio speed
        }
        else
        {
            Debug.Log("canSetPlaybackSpeed == false!!!!");
        }

        //User gaze starting position
        switch (startingHeadOrientation)
        {
            case StartingHeadOrientation.randomize90DegIntervals:
                int cameraOrientation = Random.Range(0, 3);
                cameraParent.transform.eulerAngles = new Vector3(0, 90 * cameraOrientation, 0);
                break;
            case StartingHeadOrientation.fullyRandomized:
                float pitchRotationDegrees = Random.Range(-90.0f, 90.0f);
                float yawRotationDegrees = Random.Range(-180.0f, 180.0f);
                cameraParent.transform.eulerAngles = new Vector3(pitchRotationDegrees, yawRotationDegrees, 0);
                break;
            default:            //noRotation --> nothing to be done
                break;
        }
        return startTime;
    }

    public int ChooseRandomVideo()
    {
        int chosenID;
        do
        {
            chosenID = Random.Range(0, videos.Length);
        }
        while (alreadyShownVideos[chosenID]);
        return chosenID;
    }

    public void PlayMedia(int id)                    //Use this one to play videos/audio during the experiments, starting point of the video will be decided by the user
    {
        videoPlayer.clip = videos[id].video;
        audioSource.clip = audios[id];
        float startTime = PrepareMediaToPlay(id);
        headLogger.StartLogging(id, startTime);
        eyeDataLogger.StartLogging(id, startTime);
        videoPlayer.Play();
        audioSource.Play();
    }


    private void PrepareMediaToPlayFromTimestamp(int id, float timestamp)
    {
        videoPlayer.Prepare();                  //Important if we want to seek to a random timestamp before playing the video

        //Mute audio from the video clip, ambisonic audio will be loaded and played through a different file
        for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
        {
            videoPlayer.SetDirectAudioMute(i, true);
        }

        if (!enableSound)
        {
            audioVolume = 0.0f;
        }
        audioSource.volume = audioVolume;       //We control the AudioSource volume from this component, value in range [0..1]

        if (videoPlayer.canSetTime)
        {
            videoPlayer.time = (double)timestamp;
            audioSource.time = timestamp;
            //Debug.Log("Se va a empezar la reproducción en el segundo " + (double)timestamp + " del video con duracion " + videoPlayer.clip.length);  //DEBUG
        }
        else
        {
            Debug.Log("ERROR: videoPlayer.canSetTime == false!");
        }
    }

    public void PlayMediaFromTimestamp(int id, float startingTimestamp)  //Use this one to play videos/audio when on replay mode, starting point of the video will be determined by the log loaded by the HeadDataReplayer calling this
    {
        videoPlayer.clip = videos[id].video;
        audioSource.clip = audios[id];
        PrepareMediaToPlayFromTimestamp(id, startingTimestamp);
        videoPlayer.Play();
        audioSource.Play();
    }

    // Update is called once per frame
    void Update()
    {
        RaycastHit hit;
        Debug.DrawRay(mainCamera.transform.position, mainCamera.transform.forward * 10.0f, Color.green);      //Only visible in editor window, for debugging purposes
        if(Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit, Mathf.Infinity))
        {
            if(hit.collider == cubeToTriggerPlay.GetComponent<Collider>())
            {
                lookAtObjectTimer += Time.deltaTime;
                if(lookAtObjectTimer >= timeLookingAtObjectBeforeTriggeringNextVideo)     //Time threshold to make sure the users haven't found the cube by accident, meaning they are paying attention to that specific region
                {
                    objectHasBeenFound = true;
                }
            }
        }
    }

    public bool IsReady()
    {
        return isReady;
    }
}
