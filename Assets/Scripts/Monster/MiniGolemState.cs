using UnityEngine;
using static Monster;

public static class MiniGolemState
{
    public class IdleState : BaseState
    {
        private MiniGolem miniGolem;
        public IdleState(Monster monster) : base(monster)
        {
            miniGolem = (MiniGolem)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            miniGolem.startPosition = miniGolem.transform.position;
            miniGolem.anim.SetBool("Idle", true);
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            miniGolem.moveSpeed = 0f;
            miniGolem.isWait = true; //대기상태 갱신
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            if (miniGolem.anim != null)
            {
                miniGolem.anim.SetBool("Idle", false);
            }
        }

    }

    public class TraceState : BaseState
    {
        private MiniGolem miniGolem;

        private float curWaitTime;
        public TraceState(Monster monster) : base(monster)
        {
            miniGolem = (MiniGolem)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            miniGolem.moveDirection = miniGolem.player.position.x > miniGolem.transform.position.x ? 1 : -1; //플레이어 방향으로
            miniGolem.flip(miniGolem.moveDirection);
            miniGolem.startPosition = miniGolem.transform.position;
            miniGolem.moveSpeed = miniGolem.constMoveSpeed;
            miniGolem.anim.SetBool("Run", true);
            miniGolem.isAnimeEnd = false;
            curWaitTime = 0f;
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if (curWaitTime < miniGolem.maxWaitTime)
            {
                curWaitTime += Time.deltaTime;
            }
            else
            {
                miniGolem.isAnimeEnd = true;
            }
            if (miniGolem.curMoveDistance > miniGolem.traceMoveDistance)
            {
                miniGolem.isWait = true; //대기상태 갱신
                miniGolem.curMoveDistance = 0f; // 이동 거리 초기화

                if (miniGolem.distanceToPlayer <= miniGolem.attackRadius) //현재 일반 공격 범위에 들었을때
                {
                    miniGolem.changeState(state.attack);
                }

                miniGolem.transform.position = new Vector2(miniGolem.startPosition.x + miniGolem.moveDirection * miniGolem.traceMoveDistance, miniGolem.transform.position.y);
            }

            // 플레이어 방향으로 이동
            //아주 잠깐 멈췄다가 플레이어를 향해 일정 거리를 전진하는 방식
            if (miniGolem.isWait)
            {
                miniGolem.moveDirection = miniGolem.player.position.x > miniGolem.transform.position.x ? 1 : -1; //플레이어 방향으로 이동
                miniGolem.startPosition = miniGolem.transform.position;
                miniGolem.flip(miniGolem.moveDirection);
                if (miniGolem.isGround) { miniGolem.isWait = false; }
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            miniGolem.anim.SetBool("Run", false);
        }

    }

    public class AttackState : BaseState
    {
        private MiniGolem miniGolem;
        public AttackState(Monster monster) : base(monster)
        {
            miniGolem = (MiniGolem)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            miniGolem.moveSpeed = 0f;
            miniGolem.anim.SetBool("Idle", false);
            miniGolem.anim.SetBool("Run", false);
            if (miniGolem.anim != null)
            {
                miniGolem.anim.SetBool("Attack", true);       //에니메이션에 히트박스 이벤트 설정이 되어있어서 별도로 히트박스 이벤트 작성할 필요 없음
            }
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if (miniGolem.isAnimeEnd) { miniGolem.flip(miniGolem.player.position.x > miniGolem.transform.position.x ? 1 : -1); }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            if (miniGolem.anim != null)
            {
                miniGolem.anim.SetBool("Attack", false);      //공격 애니메이션 끔
                miniGolem.isAnimeEnd = true;
            }
        }
    }
    public class JumpState : BaseState
    {
        private MiniGolem miniGolem;
        public JumpState(Monster monster) : base(monster)
        {
            miniGolem = (MiniGolem)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            miniGolem.moveSpeed = 0f;
            miniGolem.anim.SetBool("Idle", false);
            miniGolem.anim.SetBool("Run", false);
            if (miniGolem.anim != null)
            {
                miniGolem.anim.SetBool("Attack", true);       //에니메이션에 히트박스 이벤트 설정이 되어있어서 별도로 히트박스 이벤트 작성할 필요 없음
            }
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if (miniGolem.isAnimeEnd) { miniGolem.flip(miniGolem.player.position.x > miniGolem.transform.position.x ? 1 : -1); }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            if (miniGolem.anim != null)
            {
                miniGolem.anim.SetBool("Attack", false);      //공격 애니메이션 끔
                miniGolem.isAnimeEnd = true;
            }
        }
    }
}
