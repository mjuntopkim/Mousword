using UnityEngine;

public static class SkeletonState
{
    public class IdleState : BaseState
    {
        private Skeleton skeleton;
        public IdleState(Monster monster) : base(monster)
        {
            skeleton = (Skeleton)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            skeleton.startPosition = skeleton.transform.position;

            skeleton.anim.SetBool("Idle", true);
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            skeleton.moveSpeed = skeleton.constMoveSpeed;

            if (skeleton.curMoveDistance > skeleton.maxIdleMoveDistance)
            {
                skeleton.isWait = true; //대기상태 갱신
                skeleton.curMoveDistance = 0f; //이동 거리 초기화

                skeleton.transform.position = new Vector2(skeleton.startPosition.x + skeleton.moveDirection * skeleton.maxIdleMoveDistance, skeleton.transform.position.y);
            }

            if (skeleton.isWait)
            {
                if (skeleton.curWaitTime < skeleton.maxWaitTime)
                {
                    skeleton.isWait = true;
                    skeleton.curWaitTime += Time.deltaTime;
                    skeleton.anim.SetBool("Run", false);
                }
                else if (skeleton.curWaitTime >= skeleton.maxWaitTime || !skeleton.isGround)
                {
                    skeleton.moveDirection = (int)(skeleton.startPosition.x) <= (int)(skeleton.transform.position.x) ? -1 : 1; //이동 방향 반전
                    skeleton.flip(skeleton.moveDirection);                                             //좌우 반전
                    skeleton.isWait = false;
                    skeleton.curWaitTime = 0f;
                    skeleton.anim.SetBool("Run", true);
                }
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            skeleton.anim.SetBool("Idle", false);
            skeleton.anim.SetBool("Run", false);
        }

    }

    public class AttackState : BaseState
    {
        private Skeleton skeleton;

        private float attackDelayTimer = 0f;
        private const float minAttackDelay = 0.1f; // 애니메이터가 Attack 상태에서 빠져나올 최소 대기 시간

        public AttackState(Monster monster) : base(monster)
        {
            skeleton = (Skeleton)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            skeleton.isWait = true;
            attackDelayTimer = minAttackDelay; // 상태 첫 진입 시에는 바로 공격 가능
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if (skeleton.isAnimeEnd)
            {
                skeleton.anim.SetBool("Attack", false);
                skeleton.isWait = true;
                skeleton.moveDirection = skeleton.player.position.x > skeleton.transform.position.x ? 1 : -1; //플레이어 방향으로 회전
                skeleton.flip(skeleton.moveDirection);

                // 화살이 아직 남아있으면 타이머 초기화 후 대기
                if (skeleton.isAttack)
                {
                    attackDelayTimer = 0f;
                    return;
                }

                // 최소 대기 시간이 지난 뒤 다음 공격 실행
                attackDelayTimer += Time.deltaTime;
                if (attackDelayTimer >= minAttackDelay)
                {
                    skeleton.isAnimeEnd = false;
                    skeleton.isAttack = true;
                    skeleton.anim.SetBool("Attack", true);
                    attackDelayTimer = 0f;
                }
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            skeleton.anim.SetBool("Attack", false);
            skeleton.isAttack = false;
            skeleton.isAnimeEnd = true;
            skeleton.startPosition = skeleton.transform.position;
        }
    }
}
