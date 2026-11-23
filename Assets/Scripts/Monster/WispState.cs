using UnityEngine;

public static class WispState
{
    public class IdleState : BaseState
    {
        private Wisp wisp;
        private float glideTimer = 0f;   // 활공 진행 시간
        private float lastDirection = 0f; // 직전에 바라보던 방향 (방향이 바뀔 때만 뒤집기 위함)

        public IdleState(Monster monster) : base(monster)
        {
            wisp = (Wisp)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            wisp.isWait = false;
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            // 한쪽 끝 → 반대쪽 끝 = glideTime 초, 왕복 = glideTime * 2 초
            float period = Mathf.Max(wisp.glideTime, 0.01f) * 2f;
            glideTimer += Time.deltaTime;

            // 사인 곡선: 가운데서는 가장 빠르고 양 끝에서는 거의 멈춘 듯 느려짐 → 천천히 활공하는 느낌
            float phase = glideTimer / period * Mathf.PI * 2f;
            wisp.targetOffset = Vector2.right * (Mathf.Sin(phase) * wisp.HalfGlideDistance);

            // 지금 움직이는 방향(사인의 기울기)을 바라보도록 좌우 반전
            float direction = Mathf.Cos(phase) >= 0f ? 1f : -1f;
            if (direction != lastDirection)
            {
                lastDirection = direction;
                wisp.flip((int)direction);
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {

        }
    }
}
