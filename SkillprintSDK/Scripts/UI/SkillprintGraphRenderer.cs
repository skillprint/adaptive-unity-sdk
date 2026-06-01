using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Skillprint.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SkillprintGraphRenderer : MonoBehaviour
    {
        [System.Serializable]
        public class SkillNodeData
        {
            public string skillName;
            public float angleDegrees;
            public bool isActive;
            public bool isPrimary;
        }

        [Header("Graph Geometry")]
        public float graphScale = 1f;
        public float outerRadius = 200f;
        public float innerRadius = 50f;
        public float circleThickness = 2f;
        public float curveThickness = 2.5f;

        [Header("Bezier Curvature")]
        public float startTangentLength = 120f;
        public float endTangentLength = 80f;
        [Range(0f, 360f)]
        public float convergenceAngleOffset = 180f;
        public bool connectToCenter = true;
        public float curveBendAngle = 30f;

        [Header("Equidistant Spacing")]
        public bool spaceEquidistant = true;
        public float equidistantStartAngle = 0f;

        [Header("Colors")]
        public Color outerCircleColor = new Color(0.9f, 0.9f, 0.92f, 1f);
        public Color activeColor = new Color(0.6f, 0.38f, 0.93f, 1f); // Vibrant purple
        public Color inactiveColor = new Color(0.9f, 0.9f, 0.92f, 1f);
        public Color primaryBorderColor = Color.black;
        public Color centerBadgeColor = Color.white;
        public Color centerOutlineColor = new Color(0.9f, 0.9f, 0.92f, 1f);

        [Header("Center Logo Settings")]
        public Sprite centerLogoSprite;
        public float logoSize = 32f;

        [Header("Labels & Fonts")]
        public TMP_FontAsset fontAsset;
        public float labelOffset = 25f;
        public float labelWidth = 150f;
        public float labelFontSize = 14f;
        public Color labelActiveColor = new Color(0.35f, 0.35f, 0.38f, 1f);
        public Color labelInactiveColor = new Color(0.65f, 0.65f, 0.68f, 1f);

        [Header("Node Settings")]
        public float nodeSize = 14f;
        public float primaryBorderSize = 22f;
        public Sprite nodeSprite;

        [Header("Graph Background Settings")]
        public bool showBackground = true;
        public Color backgroundColor = new Color(0.98f, 0.98f, 0.99f, 1f);
        public Sprite backgroundSprite;
        public float graphPadding = 40f;

        [Header("Skills Data")]
        public List<SkillNodeData> skills = new List<SkillNodeData>();

        private Transform _nodesContainer;
        private Sprite _circleSprite;
        private Texture2D _lineTexture;

        private RectTransform _rectTransform;
        public RectTransform rectTransform
        {
            get
            {
                if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
                return _rectTransform;
            }
        }

        public Texture2D LineTexture => _lineTexture;
        public Sprite CircleSprite => _circleSprite;

        public void EnsureTextures()
        {
            if (_circleSprite == null || _lineTexture == null)
            {
                InitializeTextures();
            }
        }

        private void InitializeTextures()
        {
            if (_circleSprite == null)
            {
                int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                
                float center = (size - 1) * 0.5f;
                float radius = size * 0.5f - 1.5f;
                float fade = 1.5f;

                Color[] colors = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float alpha = Mathf.Clamp01((radius - dist) / fade);
                        colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                tex.SetPixels(colors);
                tex.Apply();
                _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }

            if (_lineTexture == null)
            {
                int size = 64;
                _lineTexture = new Texture2D(1, size, TextureFormat.RGBA32, false);
                _lineTexture.wrapMode = TextureWrapMode.Clamp;
                _lineTexture.filterMode = FilterMode.Bilinear;

                float edge = 0.15f;
                Color[] colors = new Color[size];
                for (int y = 0; y < size; y++)
                {
                    float t = y / (float)(size - 1);
                    float alpha = 1f;
                    if (t < edge) alpha = Mathf.SmoothStep(0f, 1f, t / edge);
                    else if (t > 1f - edge) alpha = Mathf.SmoothStep(0f, 1f, (1f - t) / edge);
                    
                    colors[y] = new Color(1f, 1f, 1f, alpha);
                }
                _lineTexture.SetPixels(colors);
                _lineTexture.Apply();
            }
        }

        private void Start()
        {
            Refresh();
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Wait until next frame or queue update to avoid modifying hierarchy inside serialization loop
            if (gameObject.activeInHierarchy)
            {
                UnityEditor.EditorApplication.delayCall -= DelayRebuild;
                UnityEditor.EditorApplication.delayCall += DelayRebuild;
            }
        }

        private void DelayRebuild()
        {
            if (this == null) return;
            Refresh();
            SetMeshDirty();
        }
        #endif

        public void SetMeshDirty()
        {
            var meshGo = transform.Find("VectorMesh");
            if (meshGo != null)
            {
                var meshComponent = meshGo.GetComponent<SkillprintGraphMesh>();
                if (meshComponent != null)
                {
                    meshComponent.SetAllDirty();
                }
            }
        }

        public void Refresh()
        {
            if (this == null) return;
            
            // Check if we need a structural rebuild
            bool needsRebuild = _nodesContainer == null || 
                                transform.Find("VectorMesh") == null ||
                                transform.Find("CenterHub") == null ||
                                (showBackground && transform.Find("GraphBackground") == null) ||
                                _nodesContainer.childCount != skills.Count;
            
            if (needsRebuild)
            {
                RebuildChildren();
            }
            else
            {
                UpdateAllVisuals();
            }
        }

        public void UpdateAllVisuals()
        {
            if (this == null) return;
            EnsureTextures();

            // 1. Update GraphBackground Visuals
            var bgTransform = transform.Find("GraphBackground");
            if (bgTransform != null)
            {
                var bgGo = bgTransform.gameObject;
                if (showBackground)
                {
                    var bgRect = bgGo.GetComponent<RectTransform>();
                    float bgSize = ((outerRadius + labelOffset + labelWidth) * graphScale + graphPadding) * 2f;
                    bgRect.sizeDelta = new Vector2(bgSize, bgSize);

                    var bgImage = bgGo.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.sprite = backgroundSprite;
                        if (backgroundSprite != null && backgroundSprite.name == "Background")
                        {
                            bgImage.type = Image.Type.Sliced;
                        }
                        else
                        {
                            bgImage.type = Image.Type.Simple;
                        }
                        bgImage.color = backgroundColor;
                    }
                    bgGo.SetActive(true);
                }
                else
                {
                    bgGo.SetActive(false);
                }
            }

            // 2. Update VectorMesh Visuals
            var meshTransform = transform.Find("VectorMesh");
            if (meshTransform != null)
            {
                var meshRect = meshTransform.GetComponent<RectTransform>();
                meshRect.anchorMin = Vector2.zero;
                meshRect.anchorMax = Vector2.one;
                meshRect.pivot = new Vector2(0.5f, 0.5f);
                meshRect.anchoredPosition = Vector2.zero;
                meshRect.sizeDelta = Vector2.zero;
                
                var meshComponent = meshTransform.GetComponent<SkillprintGraphMesh>();
                if (meshComponent != null)
                {
                    meshComponent.SetAllDirty();
                }
            }

            // 3. Update Center Hub Visuals
            var hubTransform = transform.Find("CenterHub");
            if (hubTransform != null)
            {
                var hubGo = hubTransform.gameObject;
                var hubRect = hubGo.GetComponent<RectTransform>();
                float hubDiameter = (innerRadius - circleThickness * 0.5f) * 2f * graphScale;
                hubRect.sizeDelta = new Vector2(hubDiameter, hubDiameter);

                var hubImage = hubGo.GetComponent<Image>();
                if (hubImage != null)
                {
                    hubImage.sprite = _circleSprite;
                    hubImage.color = centerBadgeColor;
                }

                var logoTransform = hubGo.transform.Find("Logo");
                if (logoTransform != null)
                {
                    var logoGo = logoTransform.gameObject;
                    var logoRect = logoGo.GetComponent<RectTransform>();
                    logoRect.sizeDelta = new Vector2(logoSize * graphScale, logoSize * graphScale);

                    var logoImage = logoGo.GetComponent<Image>();
                    if (logoImage != null)
                    {
                        logoImage.sprite = centerLogoSprite;
                        logoImage.color = centerLogoSprite != null ? Color.white : new Color(0, 0, 0, 0);
                    }
                }
            }

            // 4. Update Node Visuals
            if (_nodesContainer != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    if (i < _nodesContainer.childCount)
                    {
                        var nodeGo = _nodesContainer.GetChild(i).gameObject;
                        UpdateNodeVisuals(nodeGo, skills[i], i);
                    }
                }
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            Refresh();
        }

        private void Reset()
        {
            graphScale = 1f;
            outerRadius = 200f;
            innerRadius = 50f;
            circleThickness = 2f;
            curveThickness = 2.5f;
            startTangentLength = 120f;
            endTangentLength = 80f;
            convergenceAngleOffset = 180f;
            connectToCenter = true;
            curveBendAngle = 30f;
            spaceEquidistant = true;
            equidistantStartAngle = 0f;
            labelOffset = 25f;
            labelWidth = 150f;
            labelFontSize = 14f;
            nodeSize = 14f;
            primaryBorderSize = 22f;
            logoSize = 32f;
            centerBadgeColor = Color.white;
            centerOutlineColor = new Color(0.9f, 0.9f, 0.92f, 1f);
            showBackground = true;
            backgroundColor = new Color(0.98f, 0.98f, 0.99f, 1f);
            graphPadding = 40f;

            // Load default TMPro font & background sprite if available
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
            string[] logoGuids = UnityEditor.AssetDatabase.FindAssets("SkillprintLogo t:Sprite");
            if (logoGuids != null && logoGuids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(logoGuids[0]);
                centerLogoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            backgroundSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
            #endif

            skills = new List<SkillNodeData>
            {
                new SkillNodeData { skillName = "Innovate", angleDegrees = 195f, isActive = false, isPrimary = false },
                new SkillNodeData { skillName = "Relax", angleDegrees = 150f, isActive = true, isPrimary = false },
                new SkillNodeData { skillName = "Focus", angleDegrees = 125f, isActive = true, isPrimary = true },
                new SkillNodeData { skillName = "Collaborate", angleDegrees = 100f, isActive = false, isPrimary = false },
                new SkillNodeData { skillName = "Problem Solving", angleDegrees = 85f, isActive = false, isPrimary = false },
                new SkillNodeData { skillName = "Memory", angleDegrees = 70f, isActive = false, isPrimary = false },
                new SkillNodeData { skillName = "Speed", angleDegrees = 50f, isActive = false, isPrimary = false },
                new SkillNodeData { skillName = "Accuracy", angleDegrees = 35f, isActive = false, isPrimary = false },
                new SkillNodeData { skillName = "Pattern Recognition", angleDegrees = 10f, isActive = false, isPrimary = false },
                new SkillNodeData { skillName = "Spatial Awareness", angleDegrees = 345f, isActive = false, isPrimary = false },
                new SkillNodeData { skillName = "Logic", angleDegrees = 320f, isActive = false, isPrimary = false },
                new SkillNodeData { skillName = "Creativity", angleDegrees = 300f, isActive = false, isPrimary = false }
            };

            RebuildChildren();
        }

        public void RebuildChildren()
        {
            if (this == null) return;

            // Find or create container
            if (_nodesContainer == null)
            {
                var containerTransform = transform.Find("NodesContainer");
                if (containerTransform != null)
                {
                    _nodesContainer = containerTransform;
                }
                else
                {
                    var containerGo = new GameObject("NodesContainer", typeof(RectTransform));
                    containerGo.transform.SetParent(transform, false);
                    _nodesContainer = containerGo.transform;
                }
            }

            var containerRect = _nodesContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = Vector2.zero;

            // Gather existing node child objects to reuse or clean up
            var existingChildren = new List<GameObject>();
            for (int i = _nodesContainer.childCount - 1; i >= 0; i--)
            {
                existingChildren.Add(_nodesContainer.GetChild(i).gameObject);
            }

            // Sync node GameObjects with skills list
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                string targetName = $"Node_{i}_{skill.skillName}";
                GameObject nodeGo = existingChildren.Find(x => x.name == targetName);
                
                if (nodeGo != null)
                {
                    existingChildren.Remove(nodeGo);
                }
                else
                {
                    nodeGo = new GameObject(targetName, typeof(RectTransform));
                    nodeGo.transform.SetParent(_nodesContainer, false);
                }

                UpdateNodeVisuals(nodeGo, skill, i);
            }

            // Destroy unused child objects
            foreach (var child in existingChildren)
            {
                // Prevent destroying the CenterHub or GraphBackground or VectorMesh if they got swept up in cleanup
                if (child.name == "CenterHub" || child.name == "GraphBackground" || child.name == "VectorMesh") continue;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            // Find or create GraphBackground
            var bgTransform = transform.Find("GraphBackground");
            GameObject bgGo = null;
            if (showBackground)
            {
                if (bgTransform != null)
                {
                    bgGo = bgTransform.gameObject;
                }
                else
                {
                    bgGo = new GameObject("GraphBackground", typeof(RectTransform), typeof(Image));
                    bgGo.transform.SetParent(transform, false);
                }

                var bgRect = bgGo.GetComponent<RectTransform>();
                bgRect.anchorMin = new Vector2(0.5f, 0.5f);
                bgRect.anchorMax = new Vector2(0.5f, 0.5f);
                bgRect.pivot = new Vector2(0.5f, 0.5f);
                bgRect.anchoredPosition = Vector2.zero;
                float bgSize = ((outerRadius + labelOffset + labelWidth) * graphScale + graphPadding) * 2f;
                bgRect.sizeDelta = new Vector2(bgSize, bgSize);

                var bgImage = bgGo.GetComponent<Image>();
                bgImage.sprite = backgroundSprite;
                if (backgroundSprite != null && backgroundSprite.name == "Background")
                {
                    bgImage.type = Image.Type.Sliced;
                }
                else
                {
                    bgImage.type = Image.Type.Simple;
                }
                bgImage.color = backgroundColor;
                bgGo.SetActive(true);
            }
            else
            {
                if (bgTransform != null)
                {
                    bgTransform.gameObject.SetActive(false);
                }
            }

            // Find or create VectorMesh
            var meshTransform = transform.Find("VectorMesh");
            GameObject meshGo;
            if (meshTransform != null)
            {
                meshGo = meshTransform.gameObject;
            }
            else
            {
                meshGo = new GameObject("VectorMesh", typeof(RectTransform), typeof(SkillprintGraphMesh));
                meshGo.transform.SetParent(transform, false);
            }

            var meshRect = meshGo.GetComponent<RectTransform>();
            meshRect.anchorMin = Vector2.zero;
            meshRect.anchorMax = Vector2.one;
            meshRect.pivot = new Vector2(0.5f, 0.5f);
            meshRect.anchoredPosition = Vector2.zero;
            meshRect.sizeDelta = Vector2.zero;

            // Find or create CenterHub
            var hubTransform = transform.Find("CenterHub");
            GameObject hubGo;
            if (hubTransform != null)
            {
                hubGo = hubTransform.gameObject;
            }
            else
            {
                hubGo = new GameObject("CenterHub", typeof(RectTransform), typeof(Image));
                hubGo.transform.SetParent(transform, false);
            }

            var hubRect = hubGo.GetComponent<RectTransform>();
            hubRect.anchorMin = new Vector2(0.5f, 0.5f);
            hubRect.anchorMax = new Vector2(0.5f, 0.5f);
            hubRect.pivot = new Vector2(0.5f, 0.5f);
            hubRect.anchoredPosition = Vector2.zero;
            float hubDiameter = (innerRadius - circleThickness * 0.5f) * 2f * graphScale;
            hubRect.sizeDelta = new Vector2(hubDiameter, hubDiameter);

            EnsureTextures();
            var hubImage = hubGo.GetComponent<Image>();
            hubImage.sprite = _circleSprite;
            hubImage.color = centerBadgeColor;

            // Find or create Logo child inside CenterHub
            var logoTransform = hubGo.transform.Find("Logo");
            GameObject logoGo;
            if (logoTransform != null)
            {
                logoGo = logoTransform.gameObject;
            }
            else
            {
                logoGo = new GameObject("Logo", typeof(RectTransform), typeof(Image));
                logoGo.transform.SetParent(hubGo.transform, false);
            }

            var logoRect = logoGo.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 0.5f);
            logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.anchoredPosition = Vector2.zero;
            logoRect.sizeDelta = new Vector2(logoSize * graphScale, logoSize * graphScale);

            var logoImage = logoGo.GetComponent<Image>();
            logoImage.sprite = centerLogoSprite;
            logoImage.color = centerLogoSprite != null ? Color.white : new Color(0, 0, 0, 0);

            // Set correct sibling hierarchy order to enforce draw order:
            // 1. Background (at the bottom)
            // 2. VectorMesh (in the middle)
            // 3. CenterHub (on top of vector mesh)
            // 4. NodesContainer (on top of everything)
            if (bgGo != null) bgGo.transform.SetSiblingIndex(0);
            meshGo.transform.SetSiblingIndex(1);
            hubGo.transform.SetSiblingIndex(2);
            _nodesContainer.SetSiblingIndex(3);
        }

        public float GetNodeAngle(int index)
        {
            if (spaceEquidistant && skills.Count > 0)
            {
                return (index * (360f / skills.Count) + equidistantStartAngle) % 360f;
            }
            return index >= 0 && index < skills.Count ? skills[index].angleDegrees : 0f;
        }

        private void UpdateNodeVisuals(GameObject nodeGo, SkillNodeData skill, int index)
        {
            var nodeRect = nodeGo.GetComponent<RectTransform>();
            nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
            nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
            nodeRect.pivot = new Vector2(0.5f, 0.5f);

            // Calculate angle and position on outer circle
            float angleDegrees = GetNodeAngle(index);
            float angleRad = angleDegrees * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * (outerRadius * graphScale);
            nodeRect.anchoredPosition = position;

            // Normalize angle to [0, 360) to check hemisphere
            float normalizedAngle = (angleDegrees % 360f + 360f) % 360f;
            bool isLeftHemisphere = normalizedAngle > 90f && normalizedAngle < 270f;

            // Set rotation
            float targetRotation = isLeftHemisphere ? angleDegrees - 180f : angleDegrees;
            nodeRect.localRotation = Quaternion.Euler(0f, 0f, targetRotation);

            // Get or create Border
            var borderTransform = nodeGo.transform.Find("Border");
            GameObject borderGo;
            if (borderTransform != null)
            {
                borderGo = borderTransform.gameObject;
            }
            else
            {
                borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
                borderGo.transform.SetParent(nodeGo.transform, false);
            }
            EnsureTextures();
            Sprite spriteToUse = nodeSprite != null ? nodeSprite : _circleSprite;

            var borderImage = borderGo.GetComponent<Image>();
            borderImage.sprite = spriteToUse;
            borderImage.color = primaryBorderColor;

            var borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.sizeDelta = new Vector2(primaryBorderSize * graphScale, primaryBorderSize * graphScale);
            borderRect.anchoredPosition = Vector2.zero;
            borderGo.SetActive(skill.isPrimary && skill.isActive);

            // Get or create Dot
            var dotTransform = nodeGo.transform.Find("Dot");
            GameObject dotGo;
            if (dotTransform != null)
            {
                dotGo = dotTransform.gameObject;
            }
            else
            {
                dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                dotGo.transform.SetParent(nodeGo.transform, false);
            }
            var dotImage = dotGo.GetComponent<Image>();
            dotImage.sprite = spriteToUse;
            dotImage.color = skill.isActive ? activeColor : inactiveColor;

            var dotRect = dotGo.GetComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(nodeSize * graphScale, nodeSize * graphScale);
            dotRect.anchoredPosition = Vector2.zero;

            // Keep correct ordering (Border behind Dot)
            borderGo.transform.SetAsFirstSibling();
            dotGo.transform.SetAsLastSibling();

            // Get or create Text Label
            var labelTransform = nodeGo.transform.Find("Label");
            GameObject labelGo;
            if (labelTransform != null)
            {
                labelGo = labelTransform.gameObject;
            }
            else
            {
                labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(nodeGo.transform, false);
            }
            var labelText = labelGo.GetComponent<TextMeshProUGUI>();
            labelText.text = skill.skillName;
            labelText.fontSize = labelFontSize * graphScale;
            labelText.font = fontAsset;
            labelText.color = skill.isActive ? labelActiveColor : labelInactiveColor;

            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(labelWidth * graphScale, 35f * graphScale);

            if (isLeftHemisphere)
            {
                labelText.alignment = TextAlignmentOptions.Right;
                labelRect.pivot = new Vector2(1f, 0.5f);
                labelRect.anchoredPosition = new Vector2(-labelOffset * graphScale, 0f);
            }
            else
            {
                labelText.alignment = TextAlignmentOptions.Left;
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = new Vector2(labelOffset * graphScale, 0f);
            }
        }

        public void DrawMeshGeometry(VertexHelper vh)
        {
            // Draw outer and inner rings
            DrawRing(vh, outerRadius * graphScale, circleThickness * graphScale, outerCircleColor);
            DrawRing(vh, innerRadius * graphScale, circleThickness * graphScale, centerOutlineColor);

            for (int i = 0; i < skills.Count; i++)
            {
                DrawCurveForSkill(vh, skills[i], i);
            }
        }

        private void DrawRing(VertexHelper vh, float radius, float thickness, Color color)
        {
            int segments = 120;
            float renderedThickness = thickness * 1.3f;
            float rInner = radius - renderedThickness * 0.5f;
            float rOuter = radius + renderedThickness * 0.5f;

            int vertStart = vh.currentVertCount;

            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;

            Vector2 offset = GetPivotOffset();

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vert.position = new Vector3(cos * rInner + offset.x, sin * rInner + offset.y, 0f);
                vert.uv0 = new Vector2(0f, 0f);
                vh.AddVert(vert);

                vert.position = new Vector3(cos * rOuter + offset.x, sin * rOuter + offset.y, 0f);
                vert.uv0 = new Vector2(0f, 1f);
                vh.AddVert(vert);
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = vertStart + i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;

                vh.AddTriangle(i0, i1, i3);
                vh.AddTriangle(i0, i3, i2);
            }
        }

        private void DrawCurveForSkill(VertexHelper vh, SkillNodeData skill, int index)
        {
            float angleDegrees = GetNodeAngle(index);

            float convRad = convergenceAngleOffset * Mathf.Deg2Rad;
            Vector2 p0 = connectToCenter ? Vector2.zero : new Vector2(Mathf.Cos(convRad), Mathf.Sin(convRad)) * (innerRadius * graphScale);

            float endRad = angleDegrees * Mathf.Deg2Rad;
            Vector2 p3 = new Vector2(Mathf.Cos(endRad), Mathf.Sin(endRad)) * (outerRadius * graphScale);

            // Start tangent: points radially outwards from the convergence point (or rotated from node direction if center)
            Vector2 startTangentDir;
            if (connectToCenter)
            {
                float startAngleRad = endRad + curveBendAngle * Mathf.Deg2Rad;
                startTangentDir = new Vector2(Mathf.Cos(startAngleRad), Mathf.Sin(startAngleRad));
            }
            else
            {
                startTangentDir = new Vector2(Mathf.Cos(convRad), Mathf.Sin(convRad));
            }
            Vector2 p1 = p0 + startTangentDir * (startTangentLength * graphScale);

            // End tangent: enters the node radially
            Vector2 endTangentDir = new Vector2(Mathf.Cos(endRad), Mathf.Sin(endRad));
            Vector2 p2 = p3 - endTangentDir * (endTangentLength * graphScale);

            int segments = 32;
            int vertStart = vh.currentVertCount;

            UIVertex vert = UIVertex.simpleVert;
            vert.color = skill.isActive ? activeColor : inactiveColor;

            Vector2[] points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points[i] = CalculateBezierPoint(t, p0, p1, p2, p3);
            }

            Vector2 offset = GetPivotOffset();
            float renderedThickness = curveThickness * graphScale * 1.3f;

            for (int i = 0; i <= segments; i++)
            {
                Vector2 tangent;
                if (i == 0)
                {
                    tangent = (points[1] - points[0]).normalized;
                }
                else if (i == segments)
                {
                    tangent = (points[segments] - points[segments - 1]).normalized;
                }
                else
                {
                    tangent = (points[i + 1] - points[i - 1]).normalized;
                }

                Vector2 normal = new Vector2(-tangent.y, tangent.x);

                Vector2 leftPos = points[i] + normal * (renderedThickness * 0.5f) + offset;
                Vector2 rightPos = points[i] - normal * (renderedThickness * 0.5f) + offset;

                vert.position = new Vector3(leftPos.x, leftPos.y, 0f);
                vert.uv0 = new Vector2(0f, 0f);
                vh.AddVert(vert);

                vert.position = new Vector3(rightPos.x, rightPos.y, 0f);
                vert.uv0 = new Vector2(0f, 1f);
                vh.AddVert(vert);
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = vertStart + i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;

                vh.AddTriangle(i0, i1, i3);
                vh.AddTriangle(i0, i3, i2);
            }
        }

        private Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;

            return p;
        }

        private Vector2 GetPivotOffset()
        {
            var rect = rectTransform.rect;
            var pivot = rectTransform.pivot;
            return new Vector2(rect.width * (0.5f - pivot.x), rect.height * (0.5f - pivot.y));
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public class SkillprintGraphMesh : MaskableGraphic
    {
        private SkillprintGraphRenderer _parentRenderer;

        private SkillprintGraphRenderer ParentRenderer
        {
            get
            {
                if (_parentRenderer == null)
                {
                    _parentRenderer = GetComponentInParent<SkillprintGraphRenderer>();
                }
                return _parentRenderer;
            }
        }

        public override Texture mainTexture
        {
            get
            {
                if (ParentRenderer != null)
                {
                    ParentRenderer.EnsureTextures();
                    return ParentRenderer.LineTexture;
                }
                return base.mainTexture;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (ParentRenderer == null) return;

            ParentRenderer.EnsureTextures();
            ParentRenderer.DrawMeshGeometry(vh);
        }
    }
}
