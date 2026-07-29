# Skillprint Profile Integration Sample

This sample demonstrates how to retrieve and render a player's **Skillprint Profile Graph** in a Unity UI Canvas. It provides a preconfigured prefab and script components to display a circular visualization of player skills and moods.

---

## 📦 What's Included

- **`GraphView.prefab`** (`Assets/Prefabs/GraphView.prefab`): A fully styled, ready-to-use Canvas-based prefab containing the profile visualization.
- **`SkillprintProfileHarness.cs`** (included in the main SDK package under `Scripts/UI/`): A utility harness component that coordinates the backend profile/progression API requests and updates the graph renderer dynamically.
- **`SkillprintGraphRenderer.cs`** (included in the main SDK package under `Scripts/UI/`): The custom UI graphic renderer that draws the circular skill chart, nodes, and lines.

---

## 🚀 How to Run the Sample Scene

1. Open the Unity project located at `SkillprintSDK/Samples~/SkillprintProfile`.
2. Open the main sample scene (typically `Assets/Scenes/SampleScene.unity`).
3. Ensure the project dependencies (specifically **TextMeshPro** and **Unity UI**) are installed.
4. Press **Play** in the Unity Editor.
5. Click the **Load Profile** button to fetch the player profile and watch the graph render dynamically.

---

## 🛠️ Adding the Profile Graph to Your Existing Project

To use the Skillprint profile visualization in your own game:

### 1. Install the Skillprint SDK Package
Make sure the Skillprint SDK is installed in your project. If you are importing via git, add the following dependency to your project's `Packages/manifest.json`:
```json
"co.skillprint.sdk": "https://github.com/skillprint/adaptive-unity-sdk.git?path=SkillprintSDK"
```

### 2. Set Up TextMeshPro
The graph labels use **TextMeshPro (TMP)** for rendering. Ensure TMP is initialized in your project:
- Go to **Window -> TextMesh Pro -> Import TMP Essential Resources** if you haven't already.

### 3. Place the Prefab into Your Scene
1. Locate the **`GraphView.prefab`** in the samples directory (`SkillprintSDK/Samples~/SkillprintProfile/Assets/Prefabs/GraphView.prefab`).
2. Drag and drop `GraphView.prefab` directly into your active UI **Canvas** in the Hierarchy.
   > ⚠️ **Important:** The prefab must be a child of a Unity UI `Canvas` to render and receive input.

### 4. Configure the Harness Component
The root object of the `GraphView` prefab has the **`SkillprintProfileHarness`** script attached. Select the root `GraphView` object and configure the fields in the Inspector:

- **Graph Renderer**: Point this to the child `SkillprintGraph` component on the prefab (automatically set).
- **Load Profile Button**: Point this to the `LoadProfileButton` child (automatically set).
- **Custom Player ID**: Enter the unique identifier for the player whose profile you want to load (e.g., `player01@demo.skillprint.co`).
- **Custom User Token**: Optional. Direct user token to query profile data instead of exchanging a Player ID.
- **Fetch Skill Progression**: Keep this checked to query and overlay cognitive performance and skill progression data.
- **Config**: Optional. A reference to your `SkillprintConfig` asset. If assigned, the harness will automatically instantiate and initialize the `SkillprintManager` singleton at runtime if it is missing from the scene.

---

## 💻 Programmatic Usage & Customization

If you want to control the profile loading flow manually via script instead of using the default harness button:

```csharp
using UnityEngine;
using System.Collections.Generic;
using Skillprint.SDK;
using Skillprint.SDK.UI;

public class MyProfileController : MonoBehaviour
{
    [SerializeField] private SkillprintGraphRenderer graphRenderer;
    [SerializeField] private string playerId = "player@example.com";

    public void LoadAndShowProfile()
    {
        // 1. Fetch user profile (mood flow history)
        SkillprintManager.Instance.GetUserProfile(playerId, (success, profileRes) =>
        {
            if (!success || profileRes == null)
            {
                Debug.LogError("Failed to load user profile");
                return;
            }

            HashSet<string> activeSkillsAndMoods = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            // Collect active moods
            if (profileRes.results != null && profileRes.results.Count > 0)
            {
                foreach (var entry in profileRes.results[0].flowScoreHistory)
                {
                    activeSkillsAndMoods.Add(entry.targetMood);
                }
            }

            // 2. Fetch skill progression (active cognitive skills)
            SkillprintManager.Instance.GetSkillProgression(playerId, (progSuccess, progRes) =>
            {
                if (progSuccess && progRes != null && progRes.yearlySummary != null)
                {
                    foreach (var item in progRes.yearlySummary)
                    {
                        if (!string.IsNullOrEmpty(item.skill)) activeSkillsAndMoods.Add(item.skill);
                        if (!string.IsNullOrEmpty(item.mood)) activeSkillsAndMoods.Add(item.mood);
                    }
                }

                // 3. Mark matching nodes on the graph as active
                foreach (var node in graphRenderer.skills)
                {
                    node.isActive = activeSkillsAndMoods.Contains(node.skillName);
                }

                // 4. Redraw the UI Mesh
                graphRenderer.Refresh();
                graphRenderer.SetMeshDirty();
            });
        });
    }
}
```

---

## 🔍 Troubleshooting

- **Button is not interactable/clickable:**
  - Verify that the Canvas containing the prefab has a `GraphicRaycaster` component attached.
  - Verify that the parent hierarchy doesn't have an `EventSystem` missing in the scene.
  - Ensure the local scale of the root `GraphView` component is set to `(1, 1, 1)`.
- **Labels are missing or show up blank:**
  - Make sure you have imported TextMeshPro Essential Resources in your project.
  - Verify that a valid TMP Font Asset is assigned to the `Font Asset` field of the `SkillprintGraphRenderer` component.
