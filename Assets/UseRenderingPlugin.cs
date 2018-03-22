using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;


public class UseRenderingPlugin : MonoBehaviour
{
#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
    private const string dllname = "__Internal";
#else
    private const string dllname = "VlcUnityWrapper";
#endif

    // Native plugin rendering events are only called if a plugin is used
    // by some script. This means we have to DllImport at least
    // one function in some active script.
    // For this example, we'll call into plugin's SetTimeFromUnity
    // function and pass the current time so the plugin can animate.

    // Menu to select video
    public GameObject menuVideoSelector;
    public RemoteTimeDisplayer rtd;

    public int seekTimeDelta = 2000;
    // In ms

    private uint screenHeight = 720;
    private uint screenWidth = 1280;

    private uint videoHeight = 0;
    private uint videoWidth = 0;

	private Texture2D tex = null;

    // We'll also pass native pointer to a texture in Unity.
    // The plugin will fill texture data from native code.

    [DllImport (dllname)]
    private static extern void SetTextureFromUnity (System.IntPtr texture, int w, int h);

    [DllImport (dllname)]
    private static extern IntPtr GetRenderEventFunc ();

    [DllImport (dllname)]
    private static extern void launchVLC (string videoURL);

    [DllImport (dllname)]
    private static extern void stopVLC ();

    [DllImport (dllname)]
    public static extern void playPauseVLC ();

    [DllImport (dllname)]
    public static extern int getLengthVLC ();

    [DllImport (dllname)]
    public static extern int getTimeVLC ();

    [DllImport (dllname)]
    public static extern void setTimeVLC (int pos);

    [DllImport (dllname)]
    public static extern uint getVideoWidthVLC ();

    [DllImport (dllname)]
    public static extern uint getVideoHeightVLC ();

	[DllImport (dllname)]
	public static extern IntPtr getVideoFrameVLC (out bool updated);


#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport ("__Internal")]
    private static extern void RegisterPlugin ();
#endif

    public void OnMenuClick (int index)
    {
        string movieURL;

        switch (index) {
        case 1:
            movieURL = "file:///home/pierre/Videos/Night.mp4";
            break;
        case 2:
            movieURL = "file:///home/pierre/Videos/sintel_trailer-720p.mp4";
            break;
        case 3:
        default:
            movieURL = "file:///C:/Users/VLC/Videos/big_buck_bunny_480p_h264.mov";
            break;
        }

        menuVideoSelector.SetActive (false);
        videoWidth = 0;
        videoHeight = 0;
        launchVLC (movieURL);
        rtd.setPlaying (true);
        StartCoroutine ("CallPluginAtEndOfFrames");
    }

    public void playPause ()
    {
        Debug.Log ("[VLC] Toggling Play Pause !");
        playPauseVLC ();
    }

    public void stop ()
    {
        Debug.Log ("[VLC] Stopping Player !");
        rtd.setPlaying (false);
        StopCoroutine ("CallPluginAtEndOfFrames");
        stopVLC ();
        tex = null;
        GetComponent<Renderer> ().material.mainTexture = null;
        menuVideoSelector.SetActive (true);


    }

    public void seekForward ()
    {
        Debug.Log ("[VLC] Seeking forward !");
        int pos = getTimeVLC ();
        setTimeVLC (pos + seekTimeDelta);
    }

    public void seekBackward ()
    {
        Debug.Log ("[VLC] Seeking backward !");
        int pos = getTimeVLC ();
        setTimeVLC (Math.Max (0, pos - seekTimeDelta));
    }

    void Start ()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        RegisterPlugin();
#endif
        Vector3 scale = transform.localScale;
        scale.x = -scale.x;
        scale.z = -scale.z;
        transform.localScale = scale;
    }

    private IEnumerator CallPluginAtEndOfFrames ()
    {
        while (true) {
            // Wait until all frame rendering is done
            yield return new WaitForEndOfFrame ();

            // We may not receive video size the first time
            
            if (tex == null)
            {
                // If received size is not null, it and scale the texture
                uint i_videoHeight = getVideoHeightVLC();
                uint i_videoWidth = getVideoWidthVLC();
                bool updated;
                IntPtr texptr = getVideoFrameVLC(out updated);
                Debug.Log("Get video size : h:" + videoHeight + ", w:" + videoWidth);

                if (i_videoWidth != 0 && i_videoHeight != 0 && updated)
                {
                    videoWidth = i_videoWidth;
                    videoHeight = i_videoHeight;
                    tex = Texture2D.CreateExternalTexture((int)videoWidth, (int)videoHeight, TextureFormat.RGBA32, false, true, texptr);
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    GetComponent<Renderer>().material.mainTexture = tex;
                }
            }
            else if (tex != null)
            {
                bool updated;
                IntPtr texptr = getVideoFrameVLC(out updated);
                if (updated)
                {
                    Debug.Log("Update texture");
                    tex.UpdateExternalTexture(texptr);
                }
                else
                {
                    Debug.Log("texture not updated");
                }
            }

            // Issue a plugin rendering event with arbitrary integer identifier.
            GL.IssuePluginEvent (GetRenderEventFunc (), 1);
        }
    }
}