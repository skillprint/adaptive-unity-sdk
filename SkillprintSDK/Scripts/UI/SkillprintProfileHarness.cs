using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Skillprint.SDK.UI
{
    /// <summary>
    /// A modular test harness widget that fetches the user's Skillprint profile 
    /// and skill progression, then dynamically updates and redraws the SkillprintGraphRenderer.
    /// </summary>
    public class SkillprintProfileHarness : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The graph renderer instance to populate dynamically.")]
        public SkillprintGraphRenderer graphRenderer;

        [Tooltip("The button that triggers the profile API fetch when clicked.")]
        public Button loadProfileButton;

        [Header("Profile Fetch Configuration")]
        [Tooltip("The partner's player ID to fetch profile data for.")]
        public string customPlayerId = "player01@demo.skillprint.co";

        [Tooltip("If true, also retrieves and populates cognitive skills progression (e.g. Memory, Speed) alongside the profile mood history.")]
        public bool fetchSkillProgression = true;

        private void Start()
        {
            if (loadProfileButton != null)
            {
                loadProfileButton.onClick.AddListener(LoadProfile);
            }
            else
            {
                Debug.LogWarning("[SkillprintProfileHarness] Load Profile Button is not assigned.", this);
            }

            if (graphRenderer == null)
            {
                graphRenderer = GetComponent<SkillprintGraphRenderer>();
            }
        }

        /// <summary>
        /// Initiates the API request(s) to fetch user profile and progression.
        /// </summary>
        public void LoadProfile()
        {
            if (graphRenderer == null)
            {
                Debug.LogError("[SkillprintProfileHarness] SkillprintGraphRenderer is not assigned.", this);
                return;
            }

            if (Skillprint.SDK.SkillprintManager.Instance == null)
            {
                Debug.LogError("[SkillprintProfileHarness] SkillprintManager instance is not initialized.", this);
                return;
            }

            if (string.IsNullOrEmpty(customPlayerId))
            {
                Debug.LogError("[SkillprintProfileHarness] Custom Player ID cannot be empty.", this);
                return;
            }

            if (loadProfileButton != null)
            {
                loadProfileButton.interactable = false;
            }

            Debug.Log($"[SkillprintProfileHarness] Fetching profile for player: {customPlayerId}...");

            // Fetch user profile
            Skillprint.SDK.SkillprintManager.Instance.GetUserProfile(customPlayerId, (profileSuccess, profileResponse) =>
            {
                if (!profileSuccess || profileResponse == null)
                {
                    Debug.LogError("[SkillprintProfileHarness] Failed to retrieve user profile.", this);
                    ResetButtonState();
                    return;
                }

                Debug.Log("[SkillprintProfileHarness] Successfully retrieved user profile.");

                // Check if we also want to fetch skill progression details
                if (fetchSkillProgression)
                {
                    Skillprint.SDK.SkillprintManager.Instance.GetSkillProgression(customPlayerId, (progressionSuccess, progressionResponse) =>
                    {
                        ResetButtonState();
                        if (progressionSuccess && progressionResponse != null)
                        {
                            Debug.Log("[SkillprintProfileHarness] Successfully retrieved skill progression.");
                            ApplyProfileData(profileResponse, progressionResponse);
                        }
                        else
                        {
                            Debug.LogWarning("[SkillprintProfileHarness] Failed to retrieve skill progression. Populating moods only.", this);
                            ApplyProfileData(profileResponse, null);
                        }
                    });
                }
                else
                {
                    ResetButtonState();
                    ApplyProfileData(profileResponse, null);
                }
            });
        }

        private void ResetButtonState()
        {
            if (loadProfileButton != null)
            {
                loadProfileButton.interactable = true;
            }
        }

        /// <summary>
        /// Matches the active skills/moods from the API response with the graph renderer nodes.
        /// </summary>
        private void ApplyProfileData(
            Skillprint.SDK.API.UserProfileResponse profile, 
            Skillprint.SDK.API.SkillProgressionResponse progression)
        {
            if (profile.results == null || profile.results.Count == 0)
            {
                Debug.LogWarning("[SkillprintProfileHarness] Profile response does not contain any user results.", this);
                return;
            }

            var activeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Process active moods from targetMood history
            var result = profile.results[0];
            if (result.flowScoreHistory != null)
            {
                foreach (var entry in result.flowScoreHistory)
                {
                    if (!string.IsNullOrEmpty(entry.targetMood))
                    {
                        activeNames.Add(entry.targetMood);
                    }
                }
            }

            // 2. Process active skills and moods from skill progression summaries
            if (progression != null && progression.yearlySummary != null)
            {
                foreach (var item in progression.yearlySummary)
                {
                    if (!string.IsNullOrEmpty(item.skill))
                    {
                        activeNames.Add(item.skill);
                    }
                    if (!string.IsNullOrEmpty(item.mood))
                    {
                        activeNames.Add(item.mood);
                    }
                }
            }

            Debug.Log($"[SkillprintProfileHarness] Active nodes identified: {string.Join(", ", activeNames)}");

            // 3. Update the Graph Renderer node states
            bool updatedAny = false;
            foreach (var node in graphRenderer.skills)
            {
                bool previouslyActive = node.isActive;
                node.isActive = activeNames.Contains(node.skillName);
                if (previouslyActive != node.isActive)
                {
                    updatedAny = true;
                }
            }

            // 4. Force structural refresh and vector mesh redraw
            graphRenderer.Refresh();
            graphRenderer.SetMeshDirty();

            Debug.Log($"[SkillprintProfileHarness] Graph populated and redrawn (node states modified: {updatedAny}).", this);
        }

        private void OnDestroy()
        {
            if (loadProfileButton != null)
            {
                loadProfileButton.onClick.RemoveListener(LoadProfile);
            }
        }
    }
}
