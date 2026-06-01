using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Skillprint.SDK.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SkillprintWebViewWidget : MonoBehaviour
    {
        [Header("WebView Configuration")]
        [Tooltip("The base URL of the Skillprint Profile view.")]
        public string baseUrl = "https://marketplace.skillprint.co/profile";

        [Tooltip("User token to append to the URL. If empty and Resolve Token is enabled, it will be resolved at runtime.")]
        public string userToken;

        [Tooltip("User ID (Player ID) to append to the URL. If empty and Resolve User ID is enabled, it will be resolved at runtime.")]
        public string userId;

        [Header("Dynamic Resolution")]
        [Tooltip("Dynamically fetch the token from the SkillprintManager at runtime if left empty.")]
        public bool resolveTokenFromManager = true;

        [Tooltip("Dynamically fetch the player ID from the SkillprintManager or PlayerPrefs at runtime if left empty.")]
        public bool resolveUserIdFromManager = true;

        [Header("Viewport Layout")]
        [Tooltip("The RectTransform defining the screen boundaries for the WebView. If null, the widget's own RectTransform is used.")]
        public RectTransform viewportRect;

        [Header("Editor Mock Settings")]
        [Tooltip("Show the editor mock placeholder inside the Unity editor.")]
        public bool showEditorPlaceholder = true;

        private WebViewReflector _webView;
        private RectTransform _rectTransform;

        // Optimized margin tracking variables
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private Vector3[] _lastCorners = new Vector3[4];

        // Editor placeholder components
        private GameObject _placeholderGo;
        private TextMeshProUGUI _placeholderText;

        public RectTransform TargetRectTransform
        {
            get
            {
                if (viewportRect != null) return viewportRect;
                if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
                return _rectTransform;
            }
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                InitializeWebView();
            }
            else
            {
                UpdateEditorPlaceholder();
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                // Ensure placeholder is hidden/destroyed at runtime
                if (_placeholderGo != null)
                {
                    Destroy(_placeholderGo);
                }
                LoadProfileUrl();
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                if (_webView != null && _webView.IsAvailable)
                {
                    _webView.SetVisibility(false);
                }
            }
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (_webView != null)
                {
                    _webView.Destroy();
                    _webView = null;
                }
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                UpdateEditorPlaceholder();
                return;
            }

            if (_webView == null || !_webView.IsAvailable) return;

            // Check if resolution or viewport boundaries changed before recalculating margins
            bool needsMarginUpdate = false;
            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                needsMarginUpdate = true;
            }
            else
            {
                Vector3[] corners = new Vector3[4];
                TargetRectTransform.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    if (corners[i] != _lastCorners[i])
                    {
                        needsMarginUpdate = true;
                        break;
                    }
                }
            }

            if (needsMarginUpdate)
            {
                UpdateWebViewMargins();
            }
        }

        private void InitializeWebView()
        {
            _webView = new WebViewReflector(gameObject);
            if (!_webView.IsAvailable)
            {
                Debug.LogWarning("[SkillprintSDK] SkillprintWebViewWidget requires gree's unity-webview installed to overlay the profile page. Running in fallback mode.");
                return;
            }

            _webView.Init(
                cb: (msg) => Debug.Log($"[SkillprintSDK] WebView Callback: {msg}"),
                err: (msg) => Debug.LogError($"[SkillprintSDK] WebView Error: {msg}"),
                httpErr: (msg) => Debug.LogError($"[SkillprintSDK] WebView HTTP Error: {msg}")
            );

            UpdateWebViewMargins();
            _webView.SetVisibility(true);
        }

        public string BuildUrl()
        {
            string finalUrl = baseUrl;

            // 1. Resolve user token
            string resolvedToken = userToken;
            if (string.IsNullOrEmpty(resolvedToken) && resolveTokenFromManager)
            {
                resolvedToken = SkillprintManager.Instance != null ? SkillprintManager.Instance.CurrentUserToken : null;
            }

            // 2. Resolve user / player ID
            string resolvedUserId = userId;
            if (string.IsNullOrEmpty(resolvedUserId) && resolveUserIdFromManager)
            {
                resolvedUserId = SkillprintManager.Instance != null ? SkillprintManager.Instance.CurrentPlayerId : null;
                if (string.IsNullOrEmpty(resolvedUserId))
                {
                    resolvedUserId = PlayerPrefs.GetString("SkillprintPlayerId", "");
                }
            }

            // 3. Build query parameters
            List<string> queryParams = new List<string>();
            if (!string.IsNullOrEmpty(resolvedToken))
            {
                queryParams.Add($"userToken={Uri.EscapeDataString(resolvedToken)}");
            }
            if (!string.IsNullOrEmpty(resolvedUserId))
            {
                queryParams.Add($"userId={Uri.EscapeDataString(resolvedUserId)}");
            }

            if (queryParams.Count > 0)
            {
                string separator = finalUrl.Contains("?") ? "&" : "?";
                finalUrl += separator + string.Join("&", queryParams);
            }

            return finalUrl;
        }

        private void LoadProfileUrl()
        {
            if (_webView == null || !_webView.IsAvailable) return;

            string targetUrl = BuildUrl();
            Debug.Log($"[SkillprintSDK] Loading Profile URL: {targetUrl}");
            _webView.LoadURL(targetUrl);
        }

        private void UpdateWebViewMargins()
        {
            if (_webView == null || !_webView.IsAvailable) return;

            RectTransform rectTrans = TargetRectTransform;
            Vector3[] corners = new Vector3[4];
            rectTrans.GetWorldCorners(corners);

            Canvas canvas = rectTrans.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

            Vector2 screenCorner0 = RectTransformUtility.WorldToScreenPoint(cam, corners[0]); // Bottom-Left
            Vector2 screenCorner2 = RectTransformUtility.WorldToScreenPoint(cam, corners[2]); // Top-Right

            int left = Mathf.Max(0, (int)screenCorner0.x);
            int top = Mathf.Max(0, (int)(Screen.height - screenCorner2.y));
            int right = Mathf.Max(0, (int)(Screen.width - screenCorner2.x));
            int bottom = Mathf.Max(0, (int)screenCorner0.y);

            _webView.SetMargins(left, top, right, bottom);

            // Cache values
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastCorners = corners;
        }

        private void UpdateEditorPlaceholder()
        {
            if (!showEditorPlaceholder)
            {
                if (_placeholderGo != null) _placeholderGo.SetActive(false);
                return;
            }

            if (_placeholderGo == null)
            {
                Transform existing = transform.Find("EditorPlaceholder");
                if (existing != null)
                {
                    _placeholderGo = existing.gameObject;
                    _placeholderText = _placeholderGo.GetComponentInChildren<TextMeshProUGUI>();
                }
                else
                {
                    _placeholderGo = new GameObject("EditorPlaceholder", typeof(RectTransform), typeof(Image));
                    _placeholderGo.transform.SetParent(transform, false);

                    // Add text child
                    GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    textGo.transform.SetParent(_placeholderGo.transform, false);
                    _placeholderText = textGo.GetComponent<TextMeshProUGUI>();
                }
            }

            _placeholderGo.SetActive(true);

            // Style the placeholder image
            Image bgImage = _placeholderGo.GetComponent<Image>();
            bgImage.color = new Color(0.18f, 0.18f, 0.22f, 0.85f); // Elegant dark mode panel color

            // Align placeholder rect to this RectTransform
            RectTransform placeholderRect = _placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.pivot = new Vector2(0.5f, 0.5f);
            placeholderRect.anchoredPosition = Vector2.zero;
            placeholderRect.sizeDelta = Vector2.zero;

            // Style text
            if (_placeholderText != null)
            {
                RectTransform textRect = _placeholderText.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = new Vector2(-40f, -40f); // 20px padding

                _placeholderText.text = $"<b>Skillprint Profile WebView</b>\n\n" +
                                        $"<size=12><color=#A8A8A8>Calculated URL:</color>\n{BuildUrl()}\n\n" +
                                        $"<color=#BB86FC>Viewport bounds matched to {TargetRectTransform.name} (margins: left, top, right, bottom calculated dynamically at runtime)</color></size>";
                _placeholderText.fontSize = 14f;
                _placeholderText.color = Color.white;
                _placeholderText.alignment = TextAlignmentOptions.Center;
                _placeholderText.enableWordWrapping = true;
            }
        }

        // Inner class: Helper class to interact with WebViewObject via reflection
        private class WebViewReflector
        {
            private MonoBehaviour _webViewComponent;
            private Type _type;

            public bool IsAvailable => _webViewComponent != null;

            public WebViewReflector(GameObject owner)
            {
                _type = FindType("WebViewObject");
                if (_type != null)
                {
                    _webViewComponent = (MonoBehaviour)owner.AddComponent(_type);
                }
            }

            private Type FindType(string typeName)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(typeName);
                    if (type != null) return type;

                    type = assembly.GetType("Gree." + typeName);
                    if (type != null) return type;
                }
                return null;
            }

            public void Init(Action<string> cb = null, Action<string> err = null, Action<string> httpErr = null, Action<string> ld = null)
            {
                if (_webViewComponent == null) return;

                MethodInfo initMethod = _type.GetMethod("Init");
                if (initMethod != null)
                {
                    ParameterInfo[] parameters = initMethod.GetParameters();
                    object[] args = new object[parameters.Length];

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        if (parameters[i].Name == "cb") args[i] = cb;
                        else if (parameters[i].Name == "err") args[i] = err;
                        else if (parameters[i].Name == "httpErr") args[i] = httpErr;
                        else if (parameters[i].Name == "ld") args[i] = ld;
                        else
                        {
                            if (parameters[i].HasDefaultValue)
                            {
                                args[i] = parameters[i].DefaultValue;
                            }
                            else
                            {
                                if (paramType == typeof(bool)) args[i] = false;
                                else if (paramType == typeof(int)) args[i] = 100;
                                else if (paramType == typeof(string)) args[i] = "";
                                else args[i] = null;
                            }
                        }
                    }
                    initMethod.Invoke(_webViewComponent, args);
                }
            }

            public void SetMargins(int left, int top, int right, int bottom, bool relative = false)
            {
                if (_webViewComponent == null) return;

                MethodInfo setMarginsMethod = _type.GetMethod("SetMargins", new Type[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool) });
                if (setMarginsMethod != null)
                {
                    setMarginsMethod.Invoke(_webViewComponent, new object[] { left, top, right, bottom, relative });
                }
                else
                {
                    setMarginsMethod = _type.GetMethod("SetMargins", new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) });
                    if (setMarginsMethod != null)
                    {
                        setMarginsMethod.Invoke(_webViewComponent, new object[] { left, top, right, bottom });
                    }
                }
            }

            public void SetVisibility(bool visible)
            {
                if (_webViewComponent == null) return;

                MethodInfo setVisibilityMethod = _type.GetMethod("SetVisibility");
                if (setVisibilityMethod != null)
                {
                    setVisibilityMethod.Invoke(_webViewComponent, new object[] { visible });
                }
            }

            public void LoadURL(string url)
            {
                if (_webViewComponent == null) return;

                MethodInfo loadURLMethod = _type.GetMethod("LoadURL");
                if (loadURLMethod != null)
                {
                    loadURLMethod.Invoke(_webViewComponent, new object[] { url });
                }
            }

            public void Destroy()
            {
                if (_webViewComponent != null)
                {
                    UnityEngine.Object.Destroy(_webViewComponent);
                    _webViewComponent = null;
                }
            }
        }
    }
}
