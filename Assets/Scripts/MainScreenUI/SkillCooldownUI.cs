using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Mousword.MainScreenUI
{
    /// <summary>A square radial shade that clears clockwise from twelve o'clock.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillCooldownUI : MaskableGraphic
    {
        [SerializeField] private Text countdown;
        private float fraction;
        private string lastText;

        public void Configure(Text label)
        {
            countdown = label;
            color = new Color(0, 0, 0, .72f);
            raycastTarget = false;
            SetCooldown(0, 0);
        }

        // Consume authoritative gameplay values rather than running a second timer.
        public void SetCooldown(float remainingSeconds, float durationSeconds)
        {
            float next = remainingSeconds > 0 && durationSeconds > 0
                ? Mathf.Clamp01(remainingSeconds / durationSeconds) : 0;
            if (!Mathf.Approximately(fraction, next))
            {
                fraction = next;
                SetVerticesDirty();
            }
            string value = next <= 0 ? string.Empty : remainingSeconds >= 1
                ? Mathf.CeilToInt(remainingSeconds).ToString(CultureInfo.InvariantCulture)
                : remainingSeconds.ToString("0.0", CultureInfo.InvariantCulture);
            if (countdown != null && lastText != value)
            {
                countdown.text = value;
                lastText = value;
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (fraction <= 0) return;
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            Vector2 half = rect.size * .5f;
            float start = (1 - fraction) * Mathf.PI * 2;
            var tint = (Color32)color;
            mesh.AddVert(center, tint, Vector2.zero);
            AddEdge(mesh, center, half, start, tint);
            int previous = 1;
            // Global angular divisions include all four square corners exactly.
            for (int step = 1; step <= 64; step++)
            {
                float angle = step * Mathf.PI * 2 / 64;
                if (angle <= start) continue;
                AddEdge(mesh, center, half, angle, tint);
                mesh.AddTriangle(0, previous, previous + 1);
                previous++;
            }
        }

        private static void AddEdge(VertexHelper mesh, Vector2 center, Vector2 half,
            float angle, Color32 tint)
        {
            Vector2 ray = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            ray /= Mathf.Max(Mathf.Abs(ray.x), Mathf.Abs(ray.y));
            mesh.AddVert(center + Vector2.Scale(ray, half), tint, Vector2.zero);
        }
    }
}
