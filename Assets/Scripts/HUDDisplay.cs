using TMPro;
using UnityEngine;

public class HUDDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;
    [SerializeField] private Renderer backgroundRenderer;

    private static readonly Color ColorStart = Color.green;
    private static readonly Color ColorPause = Color.yellow;
    private static readonly Color ColorSafe  = new Color(1f, 0.5f, 0f);

    void Start()
    {
        if (label == null)
            label = GetComponentInChildren<TextMeshPro>();
        if (backgroundRenderer == null)
            backgroundRenderer = GetComponent<Renderer>();

        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        var mgr = RobotCommunicationManager.Instance;
        if (mgr == null) return;

        if (mgr.IsInSafePosition)
            SetHUD("Safe", ColorSafe);
        else if (mgr.IsPaused)
            SetHUD("Pause", ColorPause);
        else
            SetHUD("Start", ColorStart);
    }

    void SetHUD(string text, Color color)
    {
        if (label != null)
        {
            label.text = text;
            label.color = color;
            label.ForceMeshUpdate();
        }

        if (backgroundRenderer != null)
            backgroundRenderer.material.color = new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 1f);
    }

    void OnGUI()
    {
        var mgr = RobotCommunicationManager.Instance;
        string state = mgr == null ? "NO MANAGER"
            : mgr.IsInSafePosition ? "Safe"
            : mgr.IsPaused ? "Paused"
            : "Running";
        string labelInfo = label == null ? "label=NULL" : "label=OK";
        GUI.Label(new Rect(10, 200, 400, 25), $"HUD: {state} | {labelInfo}");
    }
}
