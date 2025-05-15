using UnityEngine;
using UnityEngine.UI; // For simple UI display
using Skillprint.SDK; // Make sure to import the Skillprint SDK namespace
using System.Collections.Generic; // For List

public class ExampleGameManager : MonoBehaviour
{
    [Header("Game Parameters (Mirrored for Display)")]
    // These are the actual variables your game would use.
    // Skillprint will modify these via the registered actions.
    public float playerSpeed = 5.0f;
    public int enemySpawnRate = 10; // e.g., spawn one enemy every 10 seconds
    public bool extraLivesEnabled = false;

    [Header("UI References (Optional for Display)")]
    public Text playerSpeedText;
    public Text enemySpawnRateText;
    public Text extraLivesEnabledText;
    public Text sessionStatusText;
    public Button startSessionButton;
    public Button stopSessionButton;

    private bool _isSessionRunning = false;

    void Start()
    {
        // Ensure SkillprintManager instance exists and config is assigned
        if (SkillprintManager.Instance == null)
        {
            Debug.LogError("SkillprintManager instance not found! Ensure it's in your scene and configured.");
            SetSessionStatusText("Error: SkillprintManager not found!");
            if (startSessionButton) startSessionButton.interactable = false;
            if (stopSessionButton) stopSessionButton.interactable = false;
            return;
        }
        if (SkillprintManager.Instance.config == null)
        {
            Debug.LogError("SkillprintConfig not assigned to SkillprintManager!");
            SetSessionStatusText("Error: SkillprintConfig not assigned!");
            if (startSessionButton) startSessionButton.interactable = false;
            if (stopSessionButton) stopSessionButton.interactable = false;
            return;
        }

        RegisterSkillprintParameters();
        SetupUI();
        UpdateParameterDisplay(); // Initial display
    }

    void RegisterSkillprintParameters()
    {
        // --- IMPORTANT ---
        // The string names used here ("playerSpeed", "enemySpawnRate", "extraLivesEnabled")
        // MUST EXACTLY MATCH the 'Parameter Name' defined in your SkillprintConfig ScriptableObject.

        SkillprintManager.Instance.RegisterParameterModifier<float>("playerSpeed", newSpeed =>
        {
            playerSpeed = newSpeed;
            Debug.Log($"[ExampleGame] Skillprint updated Player Speed to: {playerSpeed}");
            UpdateParameterDisplay();
            // In a real game, you'd apply this speed to your player character controller here.
        });

        SkillprintManager.Instance.RegisterParameterModifier<int>("enemySpawnRate", newRate =>
        {
            enemySpawnRate = newRate;
            Debug.Log($"[ExampleGame] Skillprint updated Enemy Spawn Rate to: {enemySpawnRate}");
            UpdateParameterDisplay();
            // In a real game, you'd adjust your enemy spawning logic based on this rate.
        });

        SkillprintManager.Instance.RegisterParameterModifier<bool>("extraLivesEnabled", areEnabled =>
        {
            extraLivesEnabled = areEnabled;
            Debug.Log($"[ExampleGame] Skillprint updated Extra Lives Enabled to: {extraLivesEnabled}");
            UpdateParameterDisplay();
            // In a real game, you might enable/disable an extra life system.
        });

        Debug.Log("[ExampleGame] Skillprint parameter modifiers registered.");
    }

    void SetupUI()
    {
        if (startSessionButton)
        {
            startSessionButton.onClick.AddListener(StartSkillprintSession);
            startSessionButton.interactable = !_isSessionRunning;
        }
        if (stopSessionButton)
        {
            stopSessionButton.onClick.AddListener(StopSkillprintSession);
            stopSessionButton.interactable = _isSessionRunning;
        }
        SetSessionStatusText("Session Not Started");
    }

    public void StartSkillprintSession()
    {
        if (_isSessionRunning)
        {
            Debug.LogWarning("[ExampleGame] Session is already running.");
            return;
        }

        if (SkillprintManager.Instance != null)
        {
            // You can provide a custom player ID if your game uses one.
            // string customPlayerId = "examplePlayer_12345";
            // SkillprintManager.Instance.StartGameSession(customPlayerId);

            SkillprintManager.Instance.StartGameSession(); // Start with a SDK-generated UUID for player for simplicity here
            _isSessionRunning = true;
            SetSessionStatusText($"Session Started (ID: {SkillprintManager.Instance.GetCurrentSessionId()})"); // Assuming GetCurrentSessionId() exists
            Debug.Log("[ExampleGame] Skillprint session started.");
        }
        else
        {
            Debug.LogError("[ExampleGame] SkillprintManager instance is null. Cannot start session.");
            SetSessionStatusText("Error: SkillprintManager instance is null!");
        }
        UpdateUIButtonStates();
    }

    public void StopSkillprintSession()
    {
        if (!_isSessionRunning)
        {
            Debug.LogWarning("[ExampleGame] No active session to stop.");
            return;
        }

        if (SkillprintManager.Instance != null)
        {
            SkillprintManager.Instance.StopGameSession();
            _isSessionRunning = false;
            SetSessionStatusText("Session Stopped");
            Debug.Log("[ExampleGame] Skillprint session stopped.");
        }
        else
        {
            Debug.LogError("[ExampleGame] SkillprintManager instance is null. Cannot stop session.");
        }
        UpdateUIButtonStates();
    }

    void UpdateParameterDisplay()
    {
        if (playerSpeedText) playerSpeedText.text = $"Player Speed: {playerSpeed:F2}";
        if (enemySpawnRateText) enemySpawnRateText.text = $"Enemy Spawn Rate (s): {enemySpawnRate}";
        if (extraLivesEnabledText) extraLivesEnabledText.text = $"Extra Lives Enabled: {extraLivesEnabled}";
    }

    void SetSessionStatusText(string status)
    {
        if (sessionStatusText) sessionStatusText.text = $"Status: {status}";
    }

    void UpdateUIButtonStates()
    {
        if (startSessionButton) startSessionButton.interactable = !_isSessionRunning;
        if (stopSessionButton) stopSessionButton.interactable = _isSessionRunning;
    }

    // Optional: A helper to get the current session ID if SkillprintManager exposes it
    // This is just for display purposes. Add to SkillprintManager.cs if needed:
    // public string GetCurrentSessionId() { return _currentSessionId; }

    void OnApplicationQuit()
    {
        // Ensure session is stopped if the application quits unexpectedly
        if (_isSessionRunning && SkillprintManager.Instance != null)
        {
            Debug.Log("[ExampleGame] Application quitting, stopping Skillprint session.");
            SkillprintManager.Instance.StopGameSession();
        }
    }
}