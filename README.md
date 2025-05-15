# Skillprint Unity SDK
Official Unity SDK for Skillprint integration. Empowers developers to enable dynamic gameplay adjustments based on real-time player insights.


# Skillprint Unity SDK Integration Guide

This SDK allows your Unity game to integrate with Skillprint, enabling dynamic gameplay adjustments based on real-time analysis.


## 📋 Requirements

*   Unity 2020.3 LTS or newer.
*   A Skillprint Partner API Key.
*   An active internet connection for the game client during Skillprint-enabled sessions.

---

## 🚀 Getting Started

### 1. Installation

You can install the Skillprint SDK in your Unity project using one of the following methods:

**A. Unity Package Manager (UPM) via Git URL (Recommended)**

1.  Open your Unity Project.
2.  Go to `Window -> Package Manager`.
3.  Click the `+` button in the top-left corner.
4.  Select "**Add package from git URL...**".
5.  Enter the following URL (replace `vX.Y.Z` with the desired release tag, e.g., `v1.0.0`, or use `#main` for the latest from the main branch):
    ```
    https://github.com/skillprint/skillprint-unity-sdk.git?path=/SkillprintSDK#vX.Y.Z
    ```
6.  Click "Add". The package will be installed into your project's `Packages` directory.

**B. Using `.unitypackage` File**

1.  Go to the [Releases page](https://github.com/skillprint/skillprint-unity-sdk/releases) of this repository.
2.  Download the `SkillprintSDK_vX.Y.Z.unitypackage` file from the latest (or desired) release.
3.  Open your Unity project.
4.  Drag and drop the downloaded `.unitypackage` file into your Project window, or go to `Assets -> Import Package -> Custom Package...` and select the file.
5.  Ensure all files are selected in the import dialog and click "Import". The SDK will be added to your `Assets/SkillprintSDK` folder.

### 2. Configuration

1.  **Create SkillprintConfig Asset:**
    *   In your Unity Project window, right-click: `Create -> Skillprint -> SDK Configuration`.
    *   This will create a `SkillprintConfig.asset` file. Select it.

2.  **Configure in Inspector:**
    *   **Partner API Key:** Enter your unique API key provided by Skillprint.
    *   **Skillprint API Base URL:** (Default: `https://api.skillprint.com/v1`) Adjust if you have a different endpoint.
    *   **SDK Behavior:**
        *   `Screenshot Interval Seconds`: How often to take screenshots (e.g., `2.0`).
        *   `Screenshot Post Interval Seconds`: How often to send batches of screenshots (e.g., `10.0`).
        *   `Poll Results Interval Seconds`: How often to check for parameter updates (e.g., `5.0`).
        *   `Enable Debug Logging`: Check for verbose SDK logs in the console.
    *   **Game Parameters:**
        *   Click the `+` to add parameters Skillprint can control.
        *   **Parameter Name:** A unique identifier (e.g., `playerSpeed`, `enemySpawnRate`). **This must match the name used by the Skillprint backend.**
        *   **Description:** What this variable controls (e.g., "Controls the player's movement speed.").
        *   **How SDK Changes It:** How Skillprint influences this (e.g., "Increased by Skillprint to make game faster.").
        *   **Type:** `Float`, `Integer`, or `Boolean`.
        *   **Min/Max Value:** Valid range for `Float` or `Integer`.
        *   **Default Value:** (Optional) An initial value.

    ![SkillprintConfig Inspector Example](https://via.placeholder.com/600x300.png?text=SkillprintConfig+Inspector+Screenshot)
    

3.  **Add SkillprintManager to Scene:**
    *   Create an empty GameObject in your main/first scene (e.g., name it "SkillprintService" or "Services").
    *   Add the `SkillprintManager.cs` script component to this GameObject.
        *   If installed via UPM, find it under `Packages/Skillprint SDK/Scripts/Core/`.
        *   If installed via `.unitypackage`, find it under `Assets/SkillprintSDK/Scripts/Core/`.
    *   Drag your `SkillprintConfig.asset` file from the Project window to the `Config` slot on the `SkillprintManager` component in the Inspector.

---

## 🎮 How to Use

### 1. Initialize & Register Parameter Modifiers

In a central game management script (e.g., `GameManager.cs`), obtain a reference to the `SkillprintManager` and register callback functions for each game parameter you defined in `SkillprintConfig`. This tells the SDK how to apply changes to your game variables.

This is typically done in `Awake()` or `Start()`.

```csharp
using UnityEngine;
using Skillprint.SDK; // Add this!

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
            Debug.LogError("[MyGameController] SkillprintManager instance not found! Ensure it's in your scene and configured.");
            return;
        }

        // Register a modifier for the "playerSpeed" parameter (must match SkillprintConfig)
        SkillprintManager.Instance.RegisterParameterModifier<float>("playerSpeed", newSpeed =>
        {
            currentPlayerSpeed = newSpeed;
            Debug.Log($"[MyGameController] Skillprint updated Player Speed to: {currentPlayerSpeed}");
            // Apply this newSpeed to your player character logic
        });

        // Register a modifier for the "enemyCount" parameter
        SkillprintManager.Instance.RegisterParameterModifier<int>("enemyCount", newCount =>
        {
            currentEnemyCount = newCount;
            Debug.Log($"[MyGameController] Skillprint updated Enemy Count to: {currentEnemyCount}");
            // Adjust enemy spawning or active enemies based on newCount
        });

        // Register a modifier for the "enableSpecialFeature" parameter
        SkillprintManager.Instance.RegisterParameterModifier<bool>("enableSpecialFeature", isEnabled =>
        {
            specialFeatureEnabled = isEnabled;
            Debug.Log($"[MyGameController] Skillprint updated Special Feature Enabled to: {specialFeatureEnabled}");
            // Toggle your game's special feature based on this boolean
        });
    }
}