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
   - **Game Name:** Enter your game's **slug** as registered in the Skillprint platform (e.g., `fruit-boom`, not `Fruit Boom`). This is the URL-friendly identifier for your game, not the display name. *(Required)*

   **Environment Configuration:**
   - **Target Environment:** Select `Production` or `Staging`.

   **Production API Configuration:**
   - **Partner API Key:** Enter your unique API key provided by Skillprint. *(Required)*
   - **API Base URL:** Default: `https://api.skillprint.co`. Only change this if instructed by the Skillprint team.

   **Staging API Configuration:**
   - **Partner API Key (Staging):** Enter a separate staging key, or leave blank to reuse the production key.
   - **API Base URL (Staging):** Default: `https://api.staging.skillprint.co`.

   **Gameplay Parameters:**
   - Click the `+` to add parameters Skillprint can control during sessions.
   - **Parameter Name:** A unique identifier (e.g., `playerSpeed`, `enemySpawnRate`).
   - **Description:** What this variable controls (e.g., "Controls the player's movement speed.").
   - **How SDK Changes It:** How Skillprint influences this (e.g., "Increased for grit mode, decreased for relax mode.").
   - **Type:** `Float`, `Integer`, or `Boolean`.
   - **Min/Max Value:** Valid range for `Float` or `Integer`.
   - **Default Value:** An initial/fallback value.

   > **📌 Auto-Provisioning:** Parameters defined here are **automatically registered** on the Skillprint backend the first time a session is created. You do not need to manually configure them in the admin panel. If a parameter already exists on the backend (e.g., configured by an admin with custom ranges), the SDK will not overwrite it.

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
| `creativity` | Encourage creative problem-solving and exploration |
| `collaborate` | Foster cooperative and social play dynamics |
| `joy` | Maximize fun and positive emotional experience |
| `curiosity` | Stimulate discovery and exploratory behavior |
| `empathy` | Encourage perspective-taking and emotional engagement |
| `awe` | Create moments of wonder and amazement |

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

This sends any remaining queued screenshots, signals the platform that the session is complete so final analysis can begin, and cleans up all active coroutines.

> **Important:** Always call `StopGameSession()` when gameplay ends (e.g., level complete, game over, player quits). Without this call, the session will eventually time out on the server side, but final scoring may be delayed.

### 4. Pause & Resume Screenshot Transmission

You can temporarily pause and resume the capturing and transmission (uploading) of screenshots during a session. This is particularly useful to protect player privacy on sensitive screens (e.g. settings, store/purchases, account login, enter password) or to avoid wasting network bandwidth and processing when the game is paused or idle:

```csharp
// Pause screenshot capturing and uploading
SkillprintManager.Instance.PauseScreenshotTransmission();

// Check if transmission is currently paused
bool isPaused = SkillprintManager.Instance.IsScreenshotTransmissionPaused;

// Resume screenshot capturing and uploading
SkillprintManager.Instance.ResumeScreenshotTransmission();
```

When paused:
- No new screenshots will be captured.
- No screenshot batches will be sent to the Skillprint API.
- The session remains active on the backend, and parameter polling continues normally.
- Once resumed, the SDK resumes the capture interval and transmission loops from where they left off.

---

## 📊 User Profile Graph (Visualization)

The Skillprint Unity SDK includes a modular circular profile graph component (`SkillprintGraphRenderer`) and a test harness widget (`SkillprintProfileHarness`) to retrieve player profile and progression data and visualize it dynamically in your UI.

> 💡 **Quick Start (Recommended):** A preconfigured prefab and complete integration sample are available in the [SkillprintProfile Sample directory](SkillprintSDK/Samples~/SkillprintProfile) (see its dedicated [README.md](SkillprintSDK/Samples~/SkillprintProfile/README.md) for step-by-step setup guides).

The circular profile graph displays:
- **Moods:** `Innovate`, `Relax`, `Focus`, and `Collaborate`.
- **Skills:** `Problem Solving`, `Memory`, `Speed`, `Accuracy`, `Pattern Recognition`, `Spatial Awareness`, `Logic`, and `Creativity`.

Nodes on the graph are highlighted as **active** (using your defined active color) based on whether the player has scores or gameplay history associated with those skills and moods.

### 1. Adding the Graph to a Canvas

1. In your Unity hierarchy, right-click on your Canvas and select **UI -> Panel** (or create a new GameObject with a `RectTransform` on your Canvas). Name it `SkillprintGraph`.
2. Add the `SkillprintGraphRenderer` component to `SkillprintGraph`.
3. In the Inspector, configure the visual appearance:
   - **Colors:** Set the active node color, inactive node color, and outer circle outline color.
   - **Labels & Fonts:** Assign a TMPro `Font Asset` to render the skill and mood labels.
   - **Center Logo:** Optionally assign a `Center Logo Sprite` (e.g. the Skillprint logo) to draw in the middle of the graph.
4. Click `Refresh` or drag/resize the `RectTransform` to see the preview render in the Editor.

### 2. Hooking Up the Profile Harness Widget

The SDK provides a modular `SkillprintProfileHarness` script to automatically fetch profile details from the Skillprint API and redraw the graph.

1. Create a new UI **Button** on your Canvas (e.g. named `LoadProfileButton`).
2. Attach the `SkillprintProfileHarness` component to a GameObject (or the graph itself).
3. In the Inspector, assign:
   - **Graph Renderer:** Drag your `SkillprintGraph` renderer reference here.
   - **Load Profile Button:** Drag your UI `Button` reference here.
   - **Custom Player ID:** Enter the player ID to test with (e.g. `player01@demo.skillprint.co`).
   - **Fetch Skill Progression:** Check this to fetch cognitive skill scores (like Memory, Speed) in addition to mood flow history.
4. Enter Play Mode and click the button to fetch the player's profile and see the graph update dynamically!

### 3. Programmatic Profile Retrieval & Graph Update

If you want to write custom code to load the profile and populate the graph, you can use the following API wrapper methods:

```csharp
using UnityEngine;
using Skillprint.UI;
using Skillprint.SDK;

public class ProfileController : MonoBehaviour
{
    public SkillprintGraphRenderer graphRenderer;
    public string playerId = "player-unique-id-123";

    public void RefreshProfileVisuals()
    {
        // 1. Fetch user profile (mood flow history)
        SkillprintManager.Instance.GetUserProfile(playerId, (success, profileRes) =>
        {
            if (!success || profileRes == null) return;

            // Collect active moods from history
            HashSet<string> activeNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (profileRes.results != null && profileRes.results.Count > 0)
            {
                foreach (var entry in profileRes.results[0].flowScoreHistory)
                {
                    activeNames.Add(entry.targetMood);
                }
            }

            // 2. Fetch skill progression (active cognitive skills)
            SkillprintManager.Instance.GetSkillProgression(playerId, (progSuccess, progRes) =>
            {
                if (progSuccess && progRes != null && progRes.yearlySummary != null)
                {
                    foreach (var item in progRes.yearlySummary)
                    {
                        if (!string.IsNullOrEmpty(item.skill)) activeNames.Add(item.skill);
                        if (!string.IsNullOrEmpty(item.mood)) activeNames.Add(item.mood);
                    }
                }

                // 3. Update active state of each node in the renderer
                foreach (var node in graphRenderer.skills)
                {
                    node.isActive = activeNames.Contains(node.skillName);
                }

                // 4. Force redraw
                graphRenderer.Refresh();
                graphRenderer.SetMeshDirty();
            });
        });
    }
}
```

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
StartGameSession(mood, playerId)
  ├── Create/Get User Token (if customPlayerId provided)
  ├── POST /games/api/sessions/ (create session)
  ├── Start Screenshot Capture Loop (every 2s)
  ├── Start Screenshot Post Loop (every 5s)
  │   └── POST /games/api/record-session/{sessionId}/
  └── Start Poll Results Loop (every 5s)
      └── GET /games/api/sessions/{sessionId}/
          └── Apply parameter updates via registered modifiers

StopGameSession()
  ├── Stop capture & polling coroutines
  ├── Send remaining screenshots + session close signal
  ├── Platform triggers final analysis & scoring
  └── Reset local session state
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
| "Game name is required" | Set your game's registered **slug** (e.g., `fruit-boom`) in the Game Configuration section |
| "Invalid targetMood" | Use one of: `focus`, `relax`, `grit`, `creativity`, `collaborate`, `joy`, `curiosity`, `empathy`, `awe` |
| Screenshots not uploading | Check `Enable Debug Logging` and verify internet connectivity |
| Parameters not updating | Ensure parameter names in config **exactly match** your `RegisterParameterModifier` calls (case-sensitive) |
| Parameters arriving as wrong type | The platform clamps values to your configured min/max range. Verify your parameter type and range in `SkillprintConfig` |
| Session scores not appearing | Ensure you call `StopGameSession()` — this triggers final analysis on the platform |
| WebGL URL params not detected | Verify you're running in a WebGL build (not the editor) |

### 💡 Best Practices

- **Always call `StopGameSession()`** when gameplay ends. This is required for final analysis.
- **Register modifiers before starting a session.** The SDK applies updates as soon as they arrive — if modifiers aren't registered, updates are silently dropped.
- **Use the game slug, not the display name** for the Game Name field. The slug is the URL-friendly version (lowercase, hyphens instead of spaces).
- **Parameter names are case-sensitive** and must match exactly between your `SkillprintConfig`, your `RegisterParameterModifier` calls, and the Skillprint platform configuration.
- **Keep screenshot intervals reasonable.** The default 2-second capture / 5-second upload cadence balances analysis quality with bandwidth. Shorter intervals increase accuracy but also data usage.

---

## 🎛️ Parameter Examples

Skillprint dynamically adjusts your game by modifying parameters you define. Here are common examples organized by genre to help you decide what to expose.

> 📌 **Auto-Provisioned**
>
> Parameters defined in your `SkillprintConfig` are **automatically registered** on the Skillprint backend when the first session is created. If a parameter with the same name already exists on the backend (e.g., configured by an admin with custom LLM instructions), the SDK will not overwrite it. You can fine-tune parameter behavior from the Skillprint admin panel at any time.

### Platformer / Action Game

| Parameter Name | Type | Min | Max | Default | What It Controls |
|---|---|---|---|---|---|
| `enemySpawnRate` | Float | 0.5 | 5.0 | 2.0 | Seconds between enemy spawns |
| `playerSpeed` | Float | 3.0 | 12.0 | 7.0 | Player movement speed |
| `platformGapSize` | Float | 1.0 | 6.0 | 3.0 | Distance between platforms |
| `enemyDamage` | Integer | 1 | 5 | 2 | Damage dealt by enemies per hit |
| `showHints` | Boolean | — | — | true | Whether to show contextual hints |

```csharp
SkillprintManager.Instance.RegisterParameterModifier<float>("enemySpawnRate", rate =>
{
    enemySpawner.spawnInterval = rate;
});

SkillprintManager.Instance.RegisterParameterModifier<float>("playerSpeed", speed =>
{
    playerController.moveSpeed = speed;
});

SkillprintManager.Instance.RegisterParameterModifier<bool>("showHints", show =>
{
    hintSystem.SetActive(show);
});
```

### Puzzle Game

| Parameter Name | Type | Min | Max | Default | What It Controls |
|---|---|---|---|---|---|
| `puzzleComplexity` | Integer | 1 | 10 | 5 | Number of elements in each puzzle |
| `timeLimit` | Float | 30.0 | 300.0 | 120.0 | Seconds allowed per puzzle |
| `hintCooldown` | Float | 5.0 | 60.0 | 30.0 | Seconds between available hints |
| `undoEnabled` | Boolean | — | — | true | Whether the undo button is available |

```csharp
SkillprintManager.Instance.RegisterParameterModifier<int>("puzzleComplexity", complexity =>
{
    puzzleGenerator.complexity = complexity;
    puzzleGenerator.RegenerateCurrent(); // Apply mid-session if needed
});

SkillprintManager.Instance.RegisterParameterModifier<float>("timeLimit", seconds =>
{
    timerUI.SetMaxTime(seconds);
});
```

### Shooter / Competitive Game

| Parameter Name | Type | Min | Max | Default | What It Controls |
|---|---|---|---|---|---|
| `enemyAccuracy` | Float | 0.1 | 1.0 | 0.5 | How often enemies land shots (0–1) |
| `respawnDelay` | Float | 1.0 | 10.0 | 3.0 | Seconds before player respawns |
| `ammoMultiplier` | Float | 0.5 | 3.0 | 1.0 | Multiplier for ammo pickup amounts |
| `aiAggressiveness` | Integer | 1 | 5 | 3 | How aggressively AI pursues the player |

```csharp
SkillprintManager.Instance.RegisterParameterModifier<float>("enemyAccuracy", accuracy =>
{
    foreach (var enemy in activeEnemies)
        enemy.aimAccuracy = accuracy;
});

SkillprintManager.Instance.RegisterParameterModifier<int>("aiAggressiveness", level =>
{
    aiDirector.SetAggressionLevel(level);
});
```

### How Parameters Work End-to-End

1. **You define parameters** in `SkillprintConfig` with name, type, and valid range.
2. **You register them on the Skillprint backend** (same name, type, and range) through your partner dashboard.
3. **You register modifier callbacks** in your game code via `RegisterParameterModifier<T>()`.
4. **During a session**, Skillprint analyzes gameplay screenshots and determines optimal parameter values based on the target mood and player behavior.
5. **The SDK polls for updates** and invokes your registered callbacks with the new values, automatically clamped to your defined min/max range.

> 💡 **Tip:** Start with 2–4 parameters that have the biggest impact on your game's feel. You can always add more later. Skillprint works best when parameters represent meaningful gameplay levers, not cosmetic tweaks.

---

## 📄 License

See [LICENSE](LICENSE) for details.