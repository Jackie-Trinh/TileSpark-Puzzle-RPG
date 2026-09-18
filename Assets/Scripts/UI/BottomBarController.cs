using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BottomBarController manages the bottom navigation bar on the MapScene.
/// It owns references to all three screen panels and ensures only one is
/// open at a time — clicking a button while that screen is already open
/// closes it (toggle behaviour), clicking a different button switches to it.
/// </summary>
public class BottomBarController : MonoBehaviour
{
    [Header("Screen Panels (set inactive by default in the Hierarchy)")]
    public GameObject CharacterPanel;
    public GameObject EquipmentPanel;
    public GameObject PowersPanel;

    [Header("Navigation Buttons")]
    public Button CharacterButton;
    public Button EquipmentButton;
    public Button PowersButton;

    // Which panel is currently open (null = none)
    private GameObject _openPanel;

    private void Start()
    {
        // Ensure all panels start closed
        CloseAll();

        // Wire navigation buttons
        CharacterButton?.onClick.AddListener(() => Toggle(CharacterPanel));
        EquipmentButton?.onClick.AddListener(() => Toggle(EquipmentPanel));
        PowersButton?.onClick.AddListener(()    => Toggle(PowersPanel));
    }

    /// <summary>
    /// Toggles a panel: opens it if closed, closes it if already open.
    /// Any previously open panel is closed first.
    /// </summary>
    private void Toggle(GameObject panel)
    {
        if (panel == null) return;

        if (_openPanel == panel)
        {
            // Already open — close it
            panel.SetActive(false);
            _openPanel = null;
        }
        else
        {
            // Close whatever was open, then open the requested panel
            CloseAll();
            panel.SetActive(true);
            _openPanel = panel;

            // Notify the screen component so it can refresh its content
            // (the screen may be stale if player data changed since last open)
            panel.SendMessage("OnScreenOpened", SendMessageOptions.DontRequireReceiver);
        }
    }

    /// <summary>Closes all three panels.</summary>
    public void CloseAll()
    {
        if (CharacterPanel  != null) CharacterPanel.SetActive(false);
        if (EquipmentPanel  != null) EquipmentPanel.SetActive(false);
        if (PowersPanel     != null) PowersPanel.SetActive(false);
        _openPanel = null;
    }
}
