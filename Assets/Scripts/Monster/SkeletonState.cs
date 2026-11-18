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
                    skeleton.flip(-skeleton.moveDirection);                                             //좌우 반전
                    skeleton.isWait = false;
                    skeleton.curWaitTime = 0f;
                    skeleton.anim.SetBool("Run", true);
                }
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            skeleton.anim.SetBool("Idle", false);
        }

    }

    public class AttackState : BaseState
    {
        private Skeleton skeleton;

        public AttackState(Monster monster) : base(monster)
        {
            skeleton = (Skeleton)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if (skeleton.isAnimeEnd)
            {
                skeleton.isWait = true;
                skeleton.moveDirection = skeleton.player.position.x > skeleton.transform.position.x ? 1 : -1; //플레이어 방향으로 회전
                skeleton.flip(-skeleton.moveDirection);

                //쿨타임 중
                if (skeleton.isAttack)
                {
                    skeleton.anim.SetBool("Attack", false);
                }
                else
                {
                    skeleton.isAnimeEnd = false;
                    skeleton.anim.SetBool("Attack", true);       //에니메이션에 히트박스 이벤트 설정이 되어있어서 별도로 히트박스 이벤트 작성할 필요 없음
                    skeleton.isAttack = true;
                }
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            skeleton.anim.SetBool("Attack", false);
            skeleton.isAttack = false;
            skeleton.startPosition = skeleton.transform.position;
        }

    }
}
