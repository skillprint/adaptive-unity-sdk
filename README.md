# Skillprint Unity SDK

Official Unity SDK for [Skillprint](https://www.skillprint.co) integration. Empowers game developers to enable dynamic gameplay adjustments based on real-time player skill analysis.

## 📋 Requirements

- Unity 2020.3 LTS or newer.
- A Skillprint Partner API Key (obtain from the [Skillprint Partner Portal](https://www.skillprint.co/getting-started?interest=api)).
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
   https://github.com/skillprint/adaptive-unity-sdk.git?path=SkillprintSDK#vX.Y.Z
   ```
6. Click "Add". The package will be installed into your project's `Packages` directory.

**B. Using `.unitypackage` File**

1. Go to the [Releases page](https://github.com/skillprint/adaptive-unity-sdk/releases) of this repository.
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

   **Session Defaults:**
   - **Default Target Mood:** Used when `StartGameSession()` is called without a mood, and as the fallback for URL-parameter starts. A mood passed in code or in the URL takes precedence.
   - **Optimization Target:** `Mood` (steer gameplay toward the target mood) or `Skill` (steer toward a target skill).
   - **Default Target Skill:** Shown when Optimization Target is `Skill`. The skill slug to optimize for; ask Skillprint for your game's skill slugs.

   **Environment Configuration:**
   - **Target Environment:** Select `Production` or `Staging`.

   **Authentication:**
   - **Auth Mode:** `PartnerApiKey` (default) puts your API key in the build — fine for development, but anyone can extract it from a shipped game. `TokenProvider` ships **no key**: your backend mints short-lived SDK tokens instead. **Use `TokenProvider` for anything you release.** See [Shipping without an API key](#-shipping-without-an-api-key).
   - **Token Endpoint URL:** (`TokenProvider` mode) the endpoint on your backend that mints SDK tokens. With this set, the SDK fetches tokens itself and no code is needed.

   **Production API Configuration:**
   - **Partner API Key:** Enter your unique API key provided by Skillprint. *(Required in `PartnerApiKey` mode; leave empty in `TokenProvider` mode.)*
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
// Simplest: use the Default Target Mood (and Optimization Target / Default Target Skill) from SkillprintConfig
SkillprintManager.Instance.StartGameSession();

// Or pass a target mood explicitly, with an optional player ID
SkillprintManager.Instance.StartGameSession("focus", "player-unique-id-123");

// Or optimize this session for a skill instead of a mood
SkillprintManager.Instance.StartGameSession("focus", "player-unique-id-123", targetSkill: "timing");
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

> 💡 **Note on Profile Graph Mindsets:** While starting a session uses the target moods listed above (like `creativity` or `relax`), the player's visual **User Profile Graph** displays aggregated mindsets (`Innovate`, `Relax`, `Focus`, and `Collaborate`). For example, gameplay during a session targeting `creativity` maps to the `Innovate` mindset on the profile graph.

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

### 5. Apply updates at wave or level boundaries

Parameter callbacks fire whenever a poll lands, which may be mid-wave. Games that change
settings only at natural boundaries can ignore the callbacks' timing and read the latest
targets when the next wave starts:

```csharp
// Subscribe once; fires after every poll that applied at least one update.
SkillprintManager.Instance.OnParameterUpdatesReceived += values => pendingDirty = true;

// At wave start:
if (SkillprintManager.Instance.TryGetLatestParameterValue<int>("enemyCount", out int enemies))
    nextWave.enemyCount = enemies;
```

`OnSessionStarted` and `OnSessionStartFailed` report the outcome of `StartGameSession`, and
`IsSessionActive` tells you whether a session is running. Values outside a parameter's declared
range are clamped into it (and logged) rather than dropped.

### 6. Describe a slice of play in text

Screenshots show the backend what the screen looked like; `SendTextChunk` tells it what
happened. Send the measured outcome of a wave or level and the backend's observer scores it in
text mode, then its controller decides the next parameter values with that outcome in context.
The values arrive through the normal poll a few seconds later.

```csharp
string digest =
    "wave: 7  target: focus\n" +
    "setup: 12 enemies, spawn rate 1.4/s, power-ups on\n" +
    "result: cleared in 21.4s (par 18.0s), hits taken: 2, accuracy: 0.81";
SkillprintManager.Instance.SendTextChunk(digest, "[{\"score\": 7}]", durationMs: 21400);
```

The optional second argument is a JSON array of game states; a `score` in the last one becomes
the session score.

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

## 🔐 Shipping without an API key

Your Partner API Key can create players, mint tokens for **any** of your players and read your organization's data. It belongs on your server, not in a game binary. In `TokenProvider` mode the game never sees it:

```
Game (SDK)  ──"I need a token"──▶  Your backend  ──API key──▶  Skillprint
            ◀──── spt_… token ────               ◀── token ───  POST /partners/api/sdk/token/
Game (SDK)  ──Authorization: Bearer spt_…──▶  Skillprint (sessions, screenshots, polling, profile)
```

An SDK token is bound to **one player and one game**, expires in **1 hour** by default (max 24 hours), and can only start, update and poll that player's sessions, upload their screenshots and telemetry, and read their own profile. It can't create players, mint other tokens, or read other players' data. Revoking the API key that minted it revokes the token immediately.

### 1. Add a token endpoint to your backend

Authenticate the player your own way, then exchange your API key for a token **for that player**. Never mint for a player ID the client sends without checking it.

```js
// Node/Express example
app.post("/skillprint/token", requireLoggedInPlayer, async (req, res) => {
  const r = await fetch("https://api.skillprint.co/partners/api/sdk/token/", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Api-Key": process.env.SKILLPRINT_API_KEY,   // stays on your server
    },
    body: JSON.stringify({
      internalId: req.player.id,    // YOUR player id; the player is created on first use
      game: "fruit-boom",           // your game slug
      // ttlSeconds: 3600,          // optional, 60..86400
    }),
  });
  res.status(r.status).json(await r.json()); // {"token","tokenType","expiresAt","expiresIn",...}
});
```

### 2. Point the SDK at it

Set **Auth Mode** to `TokenProvider`, clear the API key fields, and enter your endpoint in **Token Endpoint URL** on the `SkillprintConfig` asset. That is the whole integration for the common case: the SDK POSTs `{"playerId": ...}` to that URL and reads `token` / `expiresIn` from the reply, before its first request, about a minute before the token expires, and once after a `401`.

```csharp
// Nothing to call: with Token Endpoint URL set on the config asset, the SDK fetches tokens itself.
SkillprintManager.Instance.StartGameSession("focus", MyAuth.PlayerId);
```

If the request needs your own player auth header, register the provider in code instead (this takes precedence over the config field):

```csharp
SkillprintManager.Instance.SetTokenProvider(new HttpTokenProvider(
    SkillprintManager.Instance,
    "https://api.yourgame.com/skillprint/token",
    request => request.SetRequestHeader("Authorization", "Bearer " + MyAuth.SessionToken)));
```

Or plug in your own networking:

```csharp
SkillprintManager.Instance.SetTokenProvider((playerId, onComplete) =>
{
    MyBackend.GetSkillprintToken((ok, token, expiresIn, error) =>
        onComplete(ok
            ? SkillprintTokenResult.FromExpiresIn(token, expiresIn)
            : SkillprintTokenResult.Failure(error)));
});
```

If you already have a token (e.g. your web page passes one into a WebGL build), hand it over directly with `SkillprintManager.Instance.SetSdkToken(token, expiresAtUtc)`. WebGL builds also pick up an SDK token from the `userToken` URL parameter automatically.

> In `TokenProvider` mode the player is whoever the token was minted for; `customPlayerId` is passed to your provider as a hint only. `CreateOrGetUserToken` isn't available — your backend provisions players when it mints.

Release builds that still contain a Partner API Key log a warning at build time.

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

## 🔒 Privacy & Data Telemetry

To address common questions regarding player privacy, data transmission, and compliance:

- **What is captured:** The SDK only captures gameplay screenshots at the configured interval during active game sessions, along with session metadata (such as partner player ID, game slug, active parameter values, and target mood).
- **Sensitive Screens:** To protect player privacy, developers should invoke `PauseScreenshotTransmission()` when the player navigates to screens that may display sensitive input, text fields, or personal information (such as login panels, setting menus, or store/payment flows), and call `ResumeScreenshotTransmission()` when returning to gameplay.
- **Where data goes:** Screenshots and session telemetry are transmitted securely via HTTPS to the Skillprint API endpoints (`https://api.skillprint.co`).

---

## 🔑 API Endpoints

The SDK communicates with the following Skillprint API endpoints:

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/games/api/sessions/` | POST | Start a new gameplay session |
| `/games/api/record-session/{sessionId}/` | POST | Upload gameplay screenshots |
| `/games/api/sessions/{sessionId}/` | GET | Poll for parameter adjustments |
| `/scoring/api/profiles/`, `/scoring/api/skill-progression/`, `/scoring/api/mood-visualization/` | GET | Read the player's profile |
| `/partners/api/users/add/` | POST | Create a new player account *(PartnerApiKey mode only)* |
| `/partners/api/users/auth/token/` | POST | Get authentication token for a player *(PartnerApiKey mode only)* |
| `/partners/api/sdk/token/` | POST | **Your backend** mints an SDK token *(never called by the SDK)* |

In `PartnerApiKey` mode requests carry `Authorization: Api-Key <your-key>`, plus `X-Auth-Token: Token <user-token>` when a user token is available. In `TokenProvider` mode they carry only `Authorization: Bearer <sdk-token>`.

---

## 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| "SkillprintConfig not assigned" | Drag your `SkillprintConfig.asset` to the Config slot on `SkillprintManager` |
| "Partner API Key not set" | Enter your API key in the SkillprintConfig inspector, or switch Auth Mode to `TokenProvider` |
| "No SDK token available" | Auth Mode is `TokenProvider`: call `SetTokenProvider(...)` or `SetSdkToken(...)` before `StartGameSession` |
| `401` / "SDK token revoked/expired" | Your token endpoint must mint a fresh token each call; check the API key it uses is still active |
| `403` "not valid for that game" | The token was minted for a different game slug than `SkillprintConfig.gameName` |
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
2. **Parameters are auto-registered** on the Skillprint backend the first time a session is started. (Alternatively, you can manually pre-configure them via your partner dashboard to define custom LLM adjustment instructions).
3. **You register modifier callbacks** in your game code via `RegisterParameterModifier<T>()`.
4. **During a session**, Skillprint analyzes gameplay screenshots and determines optimal parameter values based on the target mood and player behavior.
5. **The SDK polls for updates** and invokes your registered callbacks with the new values, automatically clamped to your defined min/max range.

> 💡 **Tip:** Start with 2–4 parameters that have the biggest impact on your game's feel. You can always add more later. Skillprint works best when parameters represent meaningful gameplay levers, not cosmetic tweaks.

---

## 📄 License

See [LICENSE](LICENSE) for details.