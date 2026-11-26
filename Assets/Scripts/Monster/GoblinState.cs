using UnityEngine;

public static class GoblinState
{
    public class IdleState : BaseState
    {
        private Goblin goblin;
        public IdleState(Monster monster) : base(monster)
        {
            goblin = (Goblin)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            goblin.startPosition = goblin.transform.position;
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            goblin.moveSpeed = 0f;
            goblin.isWait = true; //대기상태 갱신
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            if (goblin.anim != null)
            {
                goblin.anim.SetBool("Idle", false);
            }
        }

    }

    public class NoticeState : BaseState
    {
        private Goblin goblin;
        public NoticeState(Monster monster) : base(monster)
        {
            goblin = (Goblin)monster;
        }

        // ===== [원본 코드 - 문제 있음] =====
        // 1) flip()만 호출해서 GroundCheck가 방향을 따라가지 않음 → 이전 방향의 isGround로 대기 해제 판단
        // 2) 플레이어와 x가 거의 같으면 매 프레임 방향이 바뀜
        //public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        //{
        //    goblin.moveDirection = goblin.player.position.x > goblin.transform.position.x ? 1 : -1; //플레이어 방향으로
        //    goblin.flip(goblin.moveDirection);
        //    goblin.startPosition = goblin.transform.position;
        //    goblin.moveSpeed = goblin.constMoveSpeed;
        //    goblin.anim.SetBool("Run", true);
        //}
        //
        //public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        //{
        //    if (goblin.curMoveDistance > goblin.noticeMoveDistance)
        //    {
        //        goblin.isWait = true; //대기상태 갱신
        //        goblin.curMoveDistance = 0f; // 이동 거리 초기화
        //
        //        goblin.transform.position = new Vector2(goblin.startPosition.x + goblin.moveDirection * goblin.noticeMoveDistance, goblin.transform.position.y);
        //    }
        //
        //    // 플레이어 방향으로 이동
        //    //아주 잠깐 멈췄다가 플레이어를 향해 일정 거리를 전진하는 방식
        //    if (goblin.isWait)
        //    {
        //        goblin.moveDirection = goblin.player.position.x > goblin.transform.position.x ? 1 : -1; //플레이어 방향으로 이동
        //        goblin.startPosition = goblin.transform.position;
        //        goblin.flip(goblin.moveDirection);
        //        if (goblin.isGround) { goblin.isWait = false; }
        //    }
        //}
        // ===== [원본 코드 끝] =====

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            goblin.setDirection(goblin.getDirectionToPlayer(goblin.moveDirection)); //플레이어 방향으로 (GroundCheck도 같이 이동)
            goblin.startPosition = goblin.transform.position;
            goblin.moveSpeed = goblin.constMoveSpeed;
            goblin.anim.SetBool("Run", true);
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if (goblin.curMoveDistance > goblin.noticeMoveDistance)
            {
                goblin.isWait = true; //대기상태 갱신
                goblin.curMoveDistance = 0f; // 이동 거리 초기화

                goblin.transform.position = new Vector2(goblin.startPosition.x + goblin.moveDirection * goblin.noticeMoveDistance, goblin.transform.position.y);
            }

            // 플레이어 방향으로 이동
            //아주 잠깐 멈췄다가 플레이어를 향해 일정 거리를 전진하는 방식
            if (goblin.isWait)
            {
                goblin.setDirection(goblin.getDirectionToPlayer(goblin.moveDirection)); //방향 전환 + 새 방향의 isGround 즉시 갱신
                goblin.startPosition = goblin.transform.position;
                if (goblin.isGround)
                {
                    goblin.isWait = false;
                    goblin.moveSpeed = goblin.constMoveSpeed;
                }
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            goblin.anim.SetBool("Run", false);
        }

    }

    public class AttackState : BaseState
    {
        private Goblin goblin;

        public AttackState(Monster monster) : base(monster)
        {
            goblin = (Goblin)monster;
        }

        // ===== [원본 코드 - 문제 있음] =====
        // 1) 카이팅 시작/종료가 같은 기준값(attackRadius - 0.5f)이라 경계에서 매 프레임 좌우 반전
        // 2) GroundCheck 로컬 x를 직접 옮기는데 루트 scale x가 -1이라 월드 기준 방향이 뒤집힘(도망 방향이 아닌 플레이어 쪽 검사)
        // 3) stateExit에서 GroundCheck 로컬 x를 1로 바꿔 원래 위치(-2.45)와 달라짐
        //public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        //{
        //
        //}
        //
        //public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        //{
        //    if (goblin.isAnimeEnd)
        //    {
        //        goblin.groundCheck.transform.localPosition = new Vector2((goblin.player.position.x > goblin.transform.position.x ? -1 : 1)
        //            * Mathf.Abs(goblin.groundCheck.transform.localPosition.x), goblin.groundCheck.transform.localPosition.y);
        //
        //        if (goblin.isAttack)
        //        {
        //            goblin.anim.SetBool("Attack", false);
        //            if (goblin.distanceToPlayer < goblin.attackRadius - 0.5f && goblin.isGround)
        //            {
        //                goblin.isWait = false;
        //                goblin.moveSpeed = goblin.constMoveSpeed;
        //                goblin.moveDirection = goblin.player.position.x > goblin.transform.position.x ? -1 : 1; //플레이어 반대 방향으로 카이팅
        //                goblin.flip(goblin.moveDirection);
        //                goblin.anim.SetBool("Run", true);
        //            }
        //            else if (goblin.distanceToPlayer > goblin.attackRadius - 0.5f || !goblin.isGround)
        //            {
        //                goblin.isWait = true;
        //                goblin.moveDirection = goblin.player.position.x > goblin.transform.position.x ? 1 : -1; //플레이어 방향으로 회전
        //                goblin.flip(goblin.moveDirection);
        //                goblin.anim.SetBool("Run", false);
        //            }
        //        }
        //        else
        //        {
        //            goblin.isWait = true;
        //            goblin.moveDirection = goblin.player.position.x > goblin.transform.position.x ? 1 : -1; //플레이어 방향으로 회전
        //            goblin.flip(goblin.moveDirection);
        //            goblin.isAnimeEnd = false;
        //            goblin.anim.SetBool("Attack", true);       //에니메이션에 히트박스 이벤트 설정이 되어있어서 별도로 히트박스 이벤트 작성할 필요 없음
        //            goblin.isAttack = true;
        //        }
        //    }
        //}
        //
        //public override void stateExit() //상태에 나갈 때 실행되는 코드
        //{
        //    goblin.anim.SetBool("Run", false);
        //    goblin.anim.SetBool("Attack", false);
        //    goblin.isWait = false;
        //    goblin.startPosition = goblin.transform.position;
        //    goblin.groundCheck.transform.localPosition = new Vector2(1, goblin.groundCheck.transform.localPosition.y);
        //}
        // ===== [원본 코드 끝] =====

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            goblin.isKiting = false;
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if (goblin.isAnimeEnd)
            {
                //카이팅 중이면 현재 방향의 반대가 플레이어 방향 (x 차이가 작을 때의 기본값)
                int toPlayer = goblin.getDirectionToPlayer(goblin.isKiting ? -goblin.moveDirection : goblin.moveDirection);
                int awayFromPlayer = -toPlayer;

                if (goblin.isAttack)
                {
                    goblin.anim.SetBool("Attack", false);

                    //히스테리시스: 시작 기준과 종료 기준을 다르게 둬서 경계에서 떨리지 않게 함
                    if (!goblin.isKiting)
                    {
                        //충분히 가깝고, 도망갈 쪽에 땅이 있을 때만 카이팅 시작
                        if (goblin.distanceToPlayer < goblin.attackRadius - goblin.kiteStartOffset && goblin.checkGroundAt(awayFromPlayer))
                        {
                            goblin.isKiting = true;
                        }
                    }
                    else
                    {
                        //충분히 멀어졌거나, 도망가는 방향 앞에 땅이 없으면 카이팅 종료
                        if (goblin.distanceToPlayer > goblin.attackRadius - goblin.kiteStopOffset || !goblin.isGround)
                        {
                            goblin.isKiting = false;
                        }
                    }

                    if (goblin.isKiting)
                    {
                        goblin.isWait = false;
                        goblin.moveSpeed = goblin.constMoveSpeed;
                        goblin.setDirection(awayFromPlayer);    //플레이어 반대 방향으로 카이팅 (GroundCheck도 도망 방향으로)
                        goblin.anim.SetBool("Run", true);
                    }
                    else
                    {
                        goblin.isWait = true;
                        goblin.setDirection(toPlayer);          //플레이어 방향으로 회전
                        goblin.anim.SetBool("Run", false);
                    }
                }
                else
                {
                    goblin.isKiting = false;
                    goblin.isWait = true;
                    goblin.setDirection(toPlayer);              //플레이어 방향으로 회전
                    goblin.isAnimeEnd = false;
                    goblin.anim.SetBool("Attack", true);       //에니메이션에 히트박스 이벤트 설정이 되어있어서 별도로 히트박스 이벤트 작성할 필요 없음
                    goblin.isAttack = true;
                }
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            goblin.anim.SetBool("Run", false);
            goblin.anim.SetBool("Attack", false);
            goblin.isWait = false;
            goblin.isKiting = false;
            goblin.startPosition = goblin.transform.position;
            goblin.updateGroundCheckPosition(goblin.moveDirection); //GroundCheck를 현재 방향 앞쪽으로 복구
        }

    }
}
