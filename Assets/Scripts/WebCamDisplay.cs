using UnityEngine;

public class WebCamDisplay : MonoBehaviour
{
    private WebCamTexture webcamTexture;

    void Start()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            string cameraName = devices[2].name;
            webcamTexture = new WebCamTexture(cameraName);

            Renderer renderer = GetComponent<Renderer>();
            renderer.material.mainTexture = webcamTexture;

            webcamTexture.Play();
        }
        else
        {
            Debug.LogError("No webcam found!");
        }
    }

    void OnDestroy()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
            webcamTexture.Stop();
    }
}
