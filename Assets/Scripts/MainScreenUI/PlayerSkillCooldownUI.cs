using UnityEngine;

namespace Mousword.MainScreenUI
{
    public sealed class PlayerSkillCooldownUI : MonoBehaviour
    {
        [SerializeField] private PlayerSkillController player;
        [SerializeField] private SkillCooldownUI[] slots = new SkillCooldownUI[4];
        private readonly float[] previous = new float[4];
        private readonly float[] durations = new float[4];
        private float nextLookup;

        public void Configure(PlayerSkillController target, SkillCooldownUI[] views)
        {
            player = target;
            slots = views;
        }

        private void LateUpdate()
        {
            if (player == null && PlayerLookup.TryFind(ref nextLookup, out PlayerSkillController found))
                player = found;
            if (player == null)
            {
                foreach (var slot in slots) if (slot != null) slot.SetCooldown(0, 0);
                return;
            }
            UpdateSlot(0, player.DashCooldownRemaining, player.DashSkillData);
            UpdateSlot(1, player.SpinCooldownRemaining, player.SpinSkillData);
            UpdateSlot(2, player.SwordWaveCooldownRemaining, player.SwordWaveSkillData);
            UpdateSlot(3, player.UltimateCooldownRemaining, player.UltimateSkillData);
        }

        private void UpdateSlot(int index, float remaining, SkillData data)
        {
            // A rising authoritative timer means a new cast or an external reset.
            // Snapshot its duration so editing SkillData mid-cooldown cannot jump the sweep.
            if (remaining > 0 && (previous[index] <= 0 || remaining > previous[index]))
                durations[index] = Mathf.Max(remaining, data != null ? data.coolTime : remaining);
            previous[index] = remaining;
            if (index < slots.Length && slots[index] != null)
                slots[index].SetCooldown(remaining, durations[index]);
        }
    }
}
