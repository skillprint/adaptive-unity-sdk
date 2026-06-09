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

        [Tooltip("Optional: A direct user token to fetch profile data for. If set, bypasses Player ID exchange.")]
        public string customUserToken = "";

        [Tooltip("If true, also retrieves and populates cognitive skills progression (e.g. Memory, Speed) alongside the profile mood history.")]
        public bool fetchSkillProgression = true;

        [Header("Optional Auto-Initialization")]
        [Tooltip("The SkillprintConfig scriptable object to initialize the SkillprintManager with if it is missing from the scene.")]
        public SkillprintConfig config;

        private void Start()
        {
            InitializeManagerIfNeeded();

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

        private void InitializeManagerIfNeeded()
        {
            if (Skillprint.SDK.SkillprintManager.Instance == null)
            {
                // Try to find one in the scene first
                var existingManager = FindObjectOfType<Skillprint.SDK.SkillprintManager>();
                if (existingManager != null)
                {
                    return;
                }

                if (config != null)
                {
                    Debug.Log("[SkillprintProfileHarness] SkillprintManager not found in scene. Creating one dynamically using assigned Config...");
                    GameObject managerGo = new GameObject("SkillprintManager");
                    var newManager = managerGo.AddComponent<Skillprint.SDK.SkillprintManager>();
                    newManager.Initialize(config);
                }
                else
                {
                    Debug.LogWarning("[SkillprintProfileHarness] SkillprintManager not found and no SkillprintConfig assigned in Inspector. You must have a SkillprintManager in the scene or assign a Config for auto-initialization.", this);
                }
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

            // Resolve user token from config, query parameters, or existing cached token
            string token = customUserToken;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (string.IsNullOrEmpty(token))
            {
                token = Skillprint.SDK.API.WebGLUrlParameterExtractor.GetUrlParameter("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    token = Skillprint.SDK.API.WebGLUrlParameterExtractor.GetUrlParameter("user_token");
                }
            }
#endif

            if (string.IsNullOrEmpty(token))
            {
                token = PlayerPrefs.GetString("SkillprintUserToken", string.Empty);
            }

            if (string.IsNullOrEmpty(token))
            {
                token = Skillprint.SDK.SkillprintManager.Instance.CurrentUserToken;
            }

            if (!string.IsNullOrEmpty(token))
            {
                Skillprint.SDK.SkillprintManager.Instance.CurrentUserToken = token;
            }

            if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(customPlayerId))
            {
                Debug.LogError("[SkillprintProfileHarness] Both Custom Player ID and User Token are empty. Cannot fetch profile.", this);
                return;
            }

            if (loadProfileButton != null)
            {
                loadProfileButton.interactable = false;
            }

            if (!string.IsNullOrEmpty(token))
            {
                Debug.Log("[SkillprintProfileHarness] Fetching profile data using User Token...");
            }
            else
            {
                Debug.Log($"[SkillprintProfileHarness] Fetching profile data for player: {customPlayerId}...");
            }

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
