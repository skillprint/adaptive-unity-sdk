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

            Debug.Log($"[SkillprintProfileHarness] Fetching profile data for player: {customPlayerId}...");

            var activeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int pendingRequests = fetchSkillProgression ? 3 : 2;

            Action checkComplete = () =>
            {
                pendingRequests--;
                if (pendingRequests == 0)
                {
                    ResetButtonState();
                    ApplyActiveNamesToGraph(activeNames);
                }
            };

            // 1. Fetch User Profile (for additional targetMoods in history)
            Skillprint.SDK.SkillprintManager.Instance.GetUserProfileRaw(customPlayerId, (success, json) =>
            {
                if (success && !string.IsNullOrEmpty(json))
                {
                    ExtractActiveNamesFromJson(json, activeNames);
                }
                checkComplete();
            });

            // 2. Fetch Skill Progression (for cognitive skills) if enabled
            if (fetchSkillProgression)
            {
                Skillprint.SDK.SkillprintManager.Instance.GetSkillProgressionRaw(customPlayerId, (success, json) =>
                {
                    if (success && !string.IsNullOrEmpty(json))
                    {
                        ExtractActiveNamesFromJson(json, activeNames);
                    }
                    checkComplete();
                });
            }

            // 3. Fetch Mood Visualization (for mindsets/moods)
            Skillprint.SDK.SkillprintManager.Instance.GetMoodVisualization(customPlayerId, (success, json) =>
            {
                if (success && !string.IsNullOrEmpty(json))
                {
                    ExtractActiveNamesFromJson(json, activeNames);
                }
                checkComplete();
            });
        }

        private void ExtractActiveNamesFromJson(string json, HashSet<string> activeNames)
        {
            if (string.IsNullOrEmpty(json)) return;

            // Define all known API terms that map to our 12 graph skills
            string[] knownApiTerms = new string[]
            {
                "innovate", "relax", "focus", "collaborate",
                "memory", "memorization",
                "speed", "perceptual-speed",
                "accuracy", "attention", "selective-attention",
                "pattern", "visualization", "pattern-matching", "pattern-recognition",
                "spatial", "spatial-awareness",
                "logic", "deduction", "deductive-reasoning",
                "problem-solving", "problemsolving",
                "creativity", "creative"
            };

            foreach (var term in knownApiTerms)
            {
                // We check if the JSON contains "term" with quotes to ensure it's a key or value
                string quotedTerm = "\"" + term + "\"";
                if (json.Contains(quotedTerm))
                {
                    string mappedName = MapApiTermToSkillName(term);
                    if (mappedName != null)
                    {
                        activeNames.Add(mappedName);
                    }
                }
            }
        }

        private string MapApiTermToSkillName(string term)
        {
            if (string.IsNullOrEmpty(term)) return null;

            term = term.ToLower().Trim();

            // Mindsets / Moods
            if (term.Contains("innovate")) return "Innovate";
            if (term.Contains("relax")) return "Relax";
            if (term.Contains("focus")) return "Focus";
            if (term.Contains("collaborate")) return "Collaborate";

            // Cognitive Skills
            if (term.Contains("problem-solving") || term.Contains("problemsolving")) return "Problem Solving";
            if (term.Contains("memory") || term.Contains("memorization")) return "Memory";
            if (term.Contains("speed") || term.Contains("perceptual")) return "Speed";
            if (term.Contains("accuracy") || term.Contains("attention") || term.Contains("selective")) return "Accuracy";
            if (term.Contains("pattern") || term.Contains("visualization")) return "Pattern Recognition";
            if (term.Contains("spatial")) return "Spatial Awareness";
            if (term.Contains("logic") || term.Contains("deduct")) return "Logic";
            if (term.Contains("creativ")) return "Creativity";

            return null;
        }

        private void ApplyActiveNamesToGraph(HashSet<string> activeNames)
        {
            Debug.Log($"[SkillprintProfileHarness] Active nodes identified: {string.Join(", ", activeNames)}");

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

            // Force structural refresh and vector mesh redraw
            graphRenderer.Refresh();
            graphRenderer.SetMeshDirty();

            Debug.Log($"[SkillprintProfileHarness] Graph populated and redrawn (node states modified: {updatedAny}).", this);
        }

        private void ResetButtonState()
        {
            if (loadProfileButton != null)
            {
                loadProfileButton.interactable = true;
            }
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
