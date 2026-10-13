using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single hub for every HUD/overlay panel in the GameUI prefab. Other systems
/// (GameManager, PlayerStateManager/DiedState, CheckpointManager, ExitAndEnding, ...)
/// go through here instead of each holding their own scene reference to a UI GameObject.
///
/// Auto-resolves its children by name in Awake (same hub pattern as PlayerManager for
/// the player's sub-systems), so placing this on the GameUI prefab's root needs no
/// manual wiring — inspector overrides still win if assigned.
/// </summary>
[DefaultExecutionOrder(-50)]
public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    [Header("HUD")]
    public GameObject hpContainer;
    public GameObject energyBar;
    public GameObject reticle;
    public GameObject autoAim;
    public GameObject boostOverlay;
    public TextMeshProUGUI objectiveText;

    [Header("Overlays")]
    public Image redScreenOverlay;

    [Header("Screens")]
    public GameObject pauseScreen;
    public GameObject gameOverScreen;
    public GameObject winScreen;

    [Tooltip("\"Press B to respawn\" prompt shown by OutOfBoundsWatcher. Not in the GameUI prefab yet — add a TMP text GameObject named OOBRespawnPrompt under the GameUI root.")]
    public GameObject oobRespawnPrompt;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!hpContainer) hpContainer = transform.Find("HPContainer")?.gameObject;
        if (!energyBar) energyBar = transform.Find("EnergyBar")?.gameObject;
        if (!reticle) reticle = transform.Find("Recticle")?.gameObject;
        if (!autoAim) autoAim = transform.Find("AutoAim")?.gameObject;
        if (!boostOverlay) boostOverlay = transform.Find("BoostOverlay")?.gameObject;
        if (!objectiveText) objectiveText = transform.Find("Objective/BG/ObjectiveText")?.GetComponent<TextMeshProUGUI>();
        if (!redScreenOverlay) redScreenOverlay = transform.Find("RedScreenOverlay")?.GetComponent<Image>();
        if (!pauseScreen) pauseScreen = transform.Find("PauseScreen")?.gameObject;
        if (!gameOverScreen) gameOverScreen = transform.Find("GameOverScreen")?.gameObject;
        if (!winScreen) winScreen = transform.Find("WinScreen")?.gameObject;
        if (!oobRespawnPrompt) oobRespawnPrompt = transform.Find("OOBRespawnPrompt")?.gameObject;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShowPauseScreen(bool show)
    {
        if (pauseScreen) pauseScreen.SetActive(show);
    }

    public void ShowGameOverScreen(bool show)
    {
        if (gameOverScreen) gameOverScreen.SetActive(show);
    }

    public void ShowWinScreen(bool show)
    {
        if (winScreen) winScreen.SetActive(show);
    }

    public void ShowOutOfBoundsPrompt(bool show)
    {
        if (oobRespawnPrompt) oobRespawnPrompt.SetActive(show);
    }

    public void SetObjectiveText(string text)
    {
        if (objectiveText) objectiveText.text = text;
    }

    public void ShowRedScreenOverlay(bool show)
    {
        if (redScreenOverlay) redScreenOverlay.gameObject.SetActive(show);
    }

    public void SetRedScreenAlpha(float alpha)
    {
        if (!redScreenOverlay) return;
        Color c = redScreenOverlay.color;
        c.a = alpha;
        redScreenOverlay.color = c;
    }
}
