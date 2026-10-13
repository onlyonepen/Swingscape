using System;
using UnityEngine;

/// <summary>
/// Single owner of overall game flow (menu / playing / paused) — distinct from
/// PlayerStateManager's traversal FSM (Swing/Grapple/etc), which keeps running
/// underneath this. Scoped to the gameplay scene only (same convention as
/// GlobalReference/CheckpointManager, no DontDestroyOnLoad) — the MainMenu scene
/// handles its own cursor state via MainMenu.cs.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }
    public event Action<GameState> OnGameStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        SetState(GameState.Playing);
    }

    private void Update()
    {
        if (GlobalReference.Instance == null) return;
        PlayerManager player = GlobalReference.Instance.player;
        if (player == null || player.Input == null) return;

        if (player.Input.PausePressed) TogglePause();
    }

    public void TogglePause()
    {
        if (CurrentState == GameState.Playing) Pause();
        else if (CurrentState == GameState.Paused) Resume();
    }

    public void Pause()
    {
        if (CurrentState != GameState.Playing) return;
        SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (CurrentState != GameState.Paused) return;
        SetState(GameState.Playing);
    }

    /// <summary>Called from a pause-menu "Quit to menu" button.</summary>
    public void GoToMainMenu()
    {
        SetState(GameState.MainMenu);

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene("MainMenu");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private void SetState(GameState newState)
    {
        CurrentState = newState;

        switch (newState)
        {
            case GameState.Playing:
                TimeEffects.ResetBaseTimeScale();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (GameUIManager.Instance) GameUIManager.Instance.ShowPauseScreen(false);
                break;

            case GameState.Paused:
                TimeEffects.SetBaseTimeScale(0f);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (GameUIManager.Instance) GameUIManager.Instance.ShowPauseScreen(true);
                break;

            case GameState.MainMenu:
                TimeEffects.ResetBaseTimeScale();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (GameUIManager.Instance) GameUIManager.Instance.ShowPauseScreen(false);
                break;
        }

        OnGameStateChanged?.Invoke(newState);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

public enum GameState
{
    Playing,
    Paused,
    MainMenu
}
