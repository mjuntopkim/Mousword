using UnityEngine;
using UnityEngine.UI;

namespace Mousword.MainScreenUI
{
    /// <summary>Scene-local, read-only presentation of the player's existing status.</summary>
    public sealed class PlayerMainScreenUI : MonoBehaviour
    {
        [SerializeField] private PlayerStatus player;
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private RectTransform manaFill;
        [SerializeField] private Text healthText;
        [SerializeField] private Text manaText;
        [SerializeField] private RectTransform safeArea;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        private float lastHp = float.NaN, lastMp = float.NaN;
        private float lastMaxHp = float.NaN, lastMaxMp = float.NaN;
        private float nextLookup;

        public void Configure(PlayerStatus target, RectTransform hp, RectTransform mp,
            Text hpLabel, Text mpLabel, RectTransform safe)
        {
            player = target;
            healthFill = hp;
            manaFill = mp;
            healthText = hpLabel;
            manaText = mpLabel;
            safeArea = safe;
        }

        private void LateUpdate()
        {
            ApplySafeArea();
            if (player == null && PlayerLookup.TryFind(ref nextLookup, out PlayerStatus found))
            {
                player = found;
                lastHp = lastMp = lastMaxHp = lastMaxMp = float.NaN;
            }
            if (player == null)
            {
                SetGauge(healthFill, healthText, "HP", 0, 0);
                SetGauge(manaFill, manaText, "MP", 0, 0);
                return;
            }
            if (lastHp != player.CurrentHp || lastMaxHp != player.MaxHp)
            {
                lastHp = player.CurrentHp;
                lastMaxHp = player.MaxHp;
                SetGauge(healthFill, healthText, "HP", lastHp, lastMaxHp);
            }
            if (lastMp != player.CurrentMp || lastMaxMp != player.MaxMp)
            {
                lastMp = player.CurrentMp;
                lastMaxMp = player.MaxMp;
                SetGauge(manaFill, manaText, "MP", lastMp, lastMaxMp);
            }
        }

        private static void SetGauge(RectTransform fill, Text label, string prefix, float value, float maximum)
        {
            float ratio = maximum > 0 ? Mathf.Clamp01(value / maximum) : 0;
            fill.anchorMax = new Vector2(ratio, 1);
            label.text = maximum > 0
                ? $"{prefix}   {Mathf.CeilToInt(Mathf.Clamp(value, 0, maximum))} / {Mathf.CeilToInt(maximum)}"
                : $"{prefix}   -- / --";
        }

        private void ApplySafeArea()
        {
            var dimensions = new Vector2Int(Screen.width, Screen.height);
            Rect area = Screen.safeArea;
            if (dimensions.x <= 0 || dimensions.y <= 0 ||
                (area == lastSafeArea && dimensions == lastScreen)) return;
            lastSafeArea = area;
            lastScreen = dimensions;
            safeArea.anchorMin = new Vector2(area.xMin / dimensions.x, area.yMin / dimensions.y);
            safeArea.anchorMax = new Vector2(area.xMax / dimensions.x, area.yMax / dimensions.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
    }
}
