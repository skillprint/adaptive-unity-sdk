using UnityEngine;
using UnityEngine.UI;

namespace Skillprint.UI
{
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
