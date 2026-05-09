# Skillprint Unity SDK

Official Unity SDK for [Skillprint](https://www.skillprint.co) integration. Empowers game developers to enable dynamic gameplay adjustments based on real-time player skill analysis.

## 📋 Requirements

- Unity 2020.3 LTS or newer.
- A Skillprint Partner API Key (obtain from the [Skillprint Partner Portal](https://www.skillprint.co)).
- Your game's registered **Game Name** (slug) in the Skillprint platform.
- An active internet connection for the game client during Skillprint-enabled sessions.

---

## 🚀 Getting Started

### 1. Installation

You can install the Skillprint SDK in your Unity project using one of the following methods:

**A. Unity Package Manager (UPM) via Git URL (Recommended)**

1. Open your Unity Project.
2. Go to `Window -> Package Manager`.
3. Click the `+` button in the top-left corner.
4. Select "**Add package from git URL...**".
5. Enter the following URL (replace `vX.Y.Z` with the desired release tag, e.g., `v1.0.0`, or use `#main` for the latest from the main branch):
   ```
   https://github.com/skillprint/skillprint-unity-sdk.git?path=/SkillprintSDK#vX.Y.Z
   ```
6. Click "Add". The package will be installed into your project's `Packages` directory.

**B. Using `.unitypackage` File**

1. Go to the [Releases page](https://github.com/skillprint/skillprint-unity-sdk/releases) of this repository.
2. Download the `SkillprintSDK_vX.Y.Z.unitypackage` file from the latest (or desired) release.
3. Open your Unity project.
4. Drag and drop the downloaded `.unitypackage` file into your Project window, or go to `Assets -> Import Package -> Custom Package...` and select the file.
5. Ensure all files are selected in the import dialog and click "Import". The SDK will be added to your `Assets/SkillprintSDK` folder.

### 2. Configuration

1. **Create SkillprintConfig Asset:**
   - In your Unity Project window, right-click: `Create -> Skillprint -> SDK Configuration`.
   - This will create a `SkillprintConfig.asset` file. Select it.

2. **Configure in Inspector:**

   **Game Configuration:**
   - **Game Name:** Enter your game's registered slug in the Skillprint platform. This identifies your game when communicating with the API. *(Required)*

   **Environment Configuration:**
   - **Target Environment:** Select `Production` or `Staging`.

   **Production API Configuration:**
   - **Partner API Key:** Enter your unique API key provided by Skillprint. *(Required)*
   - **API Base URL:** Default: `https://api.skillprint.co/v1`. Only change this if instructed by the Skillprint team.

   **Staging API Configuration:**
   - **Partner API Key (Staging):** Enter a separate staging key, or leave blank to reuse the production key.
   - **API Base URL (Staging):** Default: `https://api.staging.skillprint.co/v1`.

   **Gameplay Parameters:**
   - Click the `+` to add parameters Skillprint can control during sessions.
   - **Parameter Name:** A unique identifier (e.g., `playerSpeed`, `enemySpawnRate`). **This must match the name registered with the Skillprint backend.**
   - **Description:** What this variable controls (e.g., "Controls the player's movement speed.").
   - **How SDK Changes It:** How Skillprint influences this (e.g., "Increased for grit mode, decreased for relax mode.").
   - **Type:** `Float`, `Integer`, or `Boolean`.
   - **Min/Max Value:** Valid range for `Float` or `Integer`.
   - **Default Value:** An initial/fallback value.

   **SDK Behavior:**
   - `Screenshot Interval Seconds`: How often to take screenshots (default: `2.0`).
   - `Screenshot Post Interval Seconds`: How often to send batches of screenshots (default: `5.0`).
   - `Poll Results Interval Seconds`: How often to check for parameter updates (default: `5.0`).
   - `Enable Debug Logging`: Check for verbose SDK logs in the console.

3. **Add SkillprintManager to Scene:**
   - Create an empty GameObject in your main/first scene (e.g., name it "SkillprintService").
   - Add the `SkillprintManager.cs` script component to this GameObject.
     - If installed via UPM, find it under `Packages/Skillprint SDK/Scripts/Core/`.
     - If installed via `.unitypackage`, find it under `Assets/SkillprintSDK/Scripts/Core/`.
   - Drag your `SkillprintConfig.asset` file from the Project window to the **Config** slot on the `SkillprintManager` component in the Inspector.
   - The `SkillprintManager` uses `DontDestroyOnLoad` and persists across scenes automatically.

---

## 🎮 How to Use

### 1. Register Parameter Modifiers

In a central game management script (e.g., `GameManager.cs`), register callback functions for each game parameter defined in `SkillprintConfig`. This tells the SDK how to apply Skillprint's adjustments to your game variables.

```csharp
using UnityEngine;
using Skillprint.SDK;

public class MyGameController : MonoBehaviour
{
    // Your game variables that Skillprint will modify
    public float currentPlayerSpeed = 5.0f;
    public int currentEnemyCount = 10;
    public bool specialFeatureEnabled = false;

    void Start()
    {
        if (SkillprintManager.Instance == null)
        {
            Debug.LogError("SkillprintManager not found! Ensure it's in your scene.");
            return;
        }

        // Register modifiers — parameter names must match SkillprintConfig
        SkillprintManager.Instance.RegisterParameterModifier<float>("playerSpeed", newSpeed =>
        {
            currentPlayerSpeed = newSpeed;
            Debug.Log($"Skillprint updated Player Speed to: {currentPlayerSpeed}");
        });

        SkillprintManager.Instance.RegisterParameterModifier<int>("enemyCount", newCount =>
        {
            currentEnemyCount = newCount;
            Debug.Log($"Skillprint updated Enemy Count to: {currentEnemyCount}");
        });

        SkillprintManager.Instance.RegisterParameterModifier<bool>("enableSpecialFeature", isEnabled =>
        {
            specialFeatureEnabled = isEnabled;
            Debug.Log($"Skillprint updated Special Feature to: {specialFeatureEnabled}");
        });
    }
}
```

### 2. Start a Session

Start a Skillprint session when gameplay begins. The SDK will automatically capture screenshots, send them for analysis, and poll for parameter adjustments.

```csharp
// Start a session with a target mood and optional player ID
SkillprintManager.Instance.StartGameSession("focus", "player-unique-id-123");
```

**Target Moods:**
| Mood | Description |
|------|-------------|
| `focus` | Optimize for player engagement and flow state |
| `relax` | Reduce difficulty for a more casual experience |
| `grit` | Increase challenge for experienced players |

**Player Identity:**
When you provide a `customPlayerId`, the SDK automatically handles user provisioning:
1. Attempts to retrieve an existing authentication token for the player.
2. If the player doesn't exist, creates the user account automatically.
3. Attaches the user token to the session for personalized skill tracking.

If user provisioning fails, the session continues without a user token (logged as a warning).

### 3. Stop a Session

Stop the Skillprint session when gameplay ends:

```csharp
SkillprintManager.Instance.StopGameSession();
```

This cleans up all active coroutines, flushes the screenshot queue, and resets the session state.

---

## 🌐 WebGL Support

For WebGL builds, the SDK provides helper methods that automatically extract session parameters from the page URL. This is useful when Skillprint launches your game with specific parameters embedded in the URL.

```csharp
// Automatically detect mood and player ID from URL parameters
SkillprintManager.Instance.StartGameSessionFromUrl(
    fallbackMood: "relax",        // Used if no mood found in URL
    fallbackPlayerId: null        // Used if no player ID found in URL
);

// With manual overrides (useful for testing)
SkillprintManager.Instance.StartGameSessionWithOverrides(
    fallbackMood: "relax",
    fallbackPlayerId: null,
    overrideMood: "grit",             // Forces this mood regardless of URL
    overridePlayerId: "test-player"   // Forces this player ID regardless of URL
);

// Debug: inspect current URL parameters
string info = SkillprintManager.Instance.GetUrlParametersInfo();
Debug.Log(info);
```

---

## 🏗️ Architecture Overview

```
SkillprintSDK/
├── Editor/
│   ├── SkillprintConfig.asset       # Default config (create your own via menu)
│   └── SkillprintConfigEditor.cs    # Custom inspector with validation
├── Scripts/
│   ├── API/
│   │   └── SkillprintAPIClient.cs   # HTTP client (sessions, screenshots, user mgmt)
│   ├── Core/
│   │   ├── SkillprintConfig.cs      # ScriptableObject configuration
│   │   └── SkillprintManager.cs     # Singleton MonoBehaviour (main entry point)
│   └── Utilities/
│       └── ScreenshotUtility.cs     # Screen capture helper
└── Samples~/
    └── SimpleIntegration/           # Example integration scene
```

**Session Lifecycle:**

```
StartGameSession()
  ├── Create/Get User Token (if customPlayerId provided)
  ├── POST /games/api/sessions/ (start session)
  ├── Start Screenshot Capture Loop (every 2s)
  ├── Start Screenshot Post Loop (every 5s)
  │   └── POST /games/api/record-session/{sessionId}/
  └── Start Poll Results Loop (every 5s)
      └── GET /games/api/sessions/{sessionId}/
          └── Apply parameter updates via registered modifiers

StopGameSession()
  ├── Stop all coroutines
  ├── Flush screenshot queue
  └── Reset session state
```

---

## 🔑 API Endpoints

The SDK communicates with the following Skillprint API endpoints:

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/games/api/sessions/` | POST | Start a new gameplay session |
| `/games/api/record-session/{sessionId}/` | POST | Upload gameplay screenshots |
| `/games/api/sessions/{sessionId}/` | GET | Poll for parameter adjustments |
| `/partners/api/users/add/` | POST | Create a new player account |
| `/partners/api/users/auth/token/` | POST | Get authentication token for a player |

All requests include the `Authorization: Api-Key <your-key>` header. When a user token is available, it is sent as `X-Auth-Token: Token <user-token>`.

---

## 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| "SkillprintConfig not assigned" | Drag your `SkillprintConfig.asset` to the Config slot on `SkillprintManager` |
| "Partner API Key not set" | Enter your API key in the SkillprintConfig inspector |
| "Game name is required" | Set your game's registered slug in the Game Configuration section |
| "Invalid targetMood" | Use one of: `focus`, `relax`, `grit` |
| Screenshots not uploading | Check `Enable Debug Logging` and verify internet connectivity |
| Parameters not updating | Ensure parameter names match between config and `RegisterParameterModifier` calls |
| WebGL URL params not detected | Verify you're running in a WebGL build (not the editor) |

---

## 📄 License

See [LICENSE](LICENSE) for details.