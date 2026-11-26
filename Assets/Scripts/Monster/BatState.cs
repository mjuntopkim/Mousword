using UnityEngine;

public static class BatState
{
    public class IdleState : BaseState
    {
        private Bat bat;
        public IdleState(Monster monster) : base(monster)
        {
            bat = (Bat)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            bat.startPosition = bat.transform.position;
            bat.moveSpeed = 0;
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {

        }

    }
    public class AttackState : BaseState
    {
        private Bat bat;
        public AttackState(Monster monster) : base(monster)
        {
            bat = (Bat)monster;
        }

        // ===== [원본 코드 - 문제 있음] =====
        // 1) 대기 해제 직후에도 GroundCheck가 땅에 겹쳐 있으면 "|| bat.isGround" 조건으로 즉시 다시 대기 → 무한 대기
        // 2) 이전 상태의 isWait 값이 그대로 남아 공격 진입 시 바로 대기할 수 있음
        //public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        //{
        //    bat.startPosition = bat.transform.position;
        //    bat.curMoveDistance = 0f;
        //    bat.curWaitTime = 0f;
        //    bat.rotate();
        //}
        //
        //public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        //{
        //    if (!bat.isWait)
        //    {
        //        bat.moveSpeed = bat.constMoveSpeed;
        //        if (bat.anim != null) bat.anim.SetBool("Attack", true);
        //
        //        if (bat.curMoveDistance >= bat.attackMoveDistance || bat.isGround)
        //        {
        //            bat.isWait = true;
        //            bat.curMoveDistance = 0f;
        //            bat.moveSpeed = 0f;
        //
        //            bat.startPosition = bat.transform.position; // 자기 위치 저장
        //            bat.rotate();                   // 플레이어 바라봄
        //        }
        //    }
        //    else
        //    {
        //        if (bat.anim != null) bat.anim.SetBool("Attack", false);
        //
        //        if (bat.curWaitTime < bat.maxWaitTime)
        //        {
        //            bat.curWaitTime += Time.deltaTime;
        //        }
        //        else
        //        {
        //            bat.rotate();
        //            bat.startPosition = bat.transform.position;
        //            bat.isWait = false;
        //            bat.curWaitTime = 0f;
        //            bat.curWaitTime = 0f;
        //        }
        //    }
        //}
        // ===== [원본 코드 끝] =====

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            bat.startPosition = bat.transform.position;
            bat.curMoveDistance = 0f;
            bat.curWaitTime = 0f;
            bat.curDashTime = 0f;
            bat.isWait = false;             //이전 상태의 대기 값 초기화 → 진입 즉시 돌진
            bat.rotate();
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if (!bat.isWait)
            {
                bat.moveSpeed = bat.constMoveSpeed;
                if (bat.anim != null) bat.anim.SetBool("Attack", true);

                bat.curDashTime += Time.deltaTime;

                //정상 속도로 돌진 거리를 이동하는 시간의 1.5배를 넘기면 막힌 것으로 판단
                float maxDashTime = bat.constMoveSpeed > 0f ? (bat.attackMoveDistance / bat.constMoveSpeed) * 1.5f : 0f;

                bool isReached = bat.curMoveDistance >= bat.attackMoveDistance;                      //돌진 거리 도달
                bool isHitGround = bat.isGround && bat.curMoveDistance >= bat.groundIgnoreDistance;  //돌진 시작 직후의 땅 감지는 무시
                bool isTimeOut = bat.curDashTime >= maxDashTime;                                     //땅/벽에 막혀 이동하지 못함

                if (isReached || isHitGround || isTimeOut)
                {
                    bat.isWait = true;
                    bat.curMoveDistance = 0f;
                    bat.curDashTime = 0f;
                    bat.moveSpeed = 0f;

                    bat.startPosition = bat.transform.position; // 자기 위치 저장
                    bat.rotate();                   // 플레이어 바라봄
                }
            }
            else
            {
                if (bat.anim != null) bat.anim.SetBool("Attack", false);

                if (bat.curWaitTime < bat.maxWaitTime)
                {
                    bat.curWaitTime += Time.deltaTime;
                }
                else
                {
                    bat.rotate();
                    bat.startPosition = bat.transform.position;
                    bat.curMoveDistance = 0f;
                    bat.curDashTime = 0f;
                    bat.isWait = false;
                    bat.curWaitTime = 0f;
                }
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            if (bat.anim != null)
            {
                bat.anim.SetBool("Attack", false);      //공격 애니메이션 끔
            }
        }

    }
}
