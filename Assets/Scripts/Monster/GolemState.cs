using System.Runtime.CompilerServices;
using UnityEngine;
using static Monster;

public static class GolemState
{
    public class IdleState : BaseState
    {
        private Golem golem;
        public IdleState(Monster monster) : base(monster)
        {
            golem = (Golem)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            golem.startPosition = golem.transform.position;
            golem.anim.SetBool("Idle", true);
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            golem.moveSpeed = 0f;
            golem.isWait = true; //대기상태 갱신
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            if (golem.anim != null)
            {
                golem.anim.SetBool("Idle", false);
            }
        }

    }

    public class TraceState : BaseState
    {
        private Golem golem;

        private float curWaitTime;
        public TraceState(Monster monster) : base(monster)
        {
            golem = (Golem)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            golem.moveDirection = golem.player.position.x > golem.transform.position.x ? 1 : -1; //플레이어 방향으로
            golem.flip(golem.moveDirection);
            golem.startPosition = golem.transform.position;
            golem.moveSpeed = golem.constMoveSpeed;
            golem.anim.SetBool("Run", true);
            golem.isAnimeEnd = false;
            curWaitTime = 0f;
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if(curWaitTime < golem.maxWaitTime)
            {
                curWaitTime += Time.deltaTime;
            }
            else
            {
                golem.isAnimeEnd = true;
            }
            if (golem.curMoveDistance > golem.traceMoveDistance)
            {
                golem.isWait = true; //대기상태 갱신
                golem.curMoveDistance = 0f; // 이동 거리 초기화

                if (golem.distanceToPlayer <= golem.attackRadius && !golem.isJumping) //현재 일반 공격 범위에 들었을때
                {
                    golem.changeState(state.smash);
                }

                golem.transform.position = new Vector2(golem.startPosition.x + golem.moveDirection * golem.traceMoveDistance, golem.transform.position.y);
            }

            // 플레이어 방향으로 이동
            //아주 잠깐 멈췄다가 플레이어를 향해 일정 거리를 전진하는 방식
            if (golem.isWait)
            {
                golem.moveDirection = golem.player.position.x > golem.transform.position.x ? 1 : -1; //플레이어 방향으로 이동
                golem.startPosition = golem.transform.position;
                golem.flip(golem.moveDirection);
                if (golem.isGround) { golem.isWait = false; }
            }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            golem.anim.SetBool("Run", false);
        }

    }

    public class SmashState : BaseState
    {
        private Golem golem;

        public SmashState(Monster monster) : base(monster)
        {
            golem = (Golem)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            golem.moveSpeed = 0f;
            golem.anim.SetBool("Idle", false);
            golem.anim.SetBool("Run", false);
            if (golem.anim != null)
            {
                golem.anim.SetBool("Smash", true);       //에니메이션에 히트박스 이벤트 설정이 되어있어서 별도로 히트박스 이벤트 작성할 필요 없음
            }
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {
            if (golem.isAnimeEnd) { golem.flip(golem.player.position.x > golem.transform.position.x ? 1 : -1); }
        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            if (golem.anim != null)
            {
                golem.anim.SetBool("Smash", false);      //공격 애니메이션 끔
                golem.isAnimeEnd = true;
            }
        }

    }

    public class JumpState : BaseState
    {
        private Golem golem;

        private const float jumpHeightAbovePlayer = 4f; // 시전 당시 플레이어보다 4칸 위
        private const float minArcOffset = 0.5f;        // 골렘이 이미 더 높을 때의 최소 상승 여유치
        private const float minAirTime = 0.1f;          // 이륙 직후 착지 오판정 방지 시간

        private bool hasLeftGround;
        private bool hasLanded;
        private float elapsedTime;
        private float expectedFlightTime;
        private float targetVelocityX;
        private float savedLinearDamping;

        public JumpState(Monster monster) : base(monster)
        {
            golem = (Golem)monster;
        }

        public override void stateEnter()
        {
            if (golem.anim != null)
            {
                golem.anim.SetBool("Idle", false);
                golem.anim.SetBool("Run", false);
                golem.anim.SetBool("Jump", true);
            }

            golem.isAnimeEnd = false;
            golem.isJumping = true;
            golem.isWait = false;

            hasLeftGround = false;
            hasLanded = false;
            elapsedTime = 0f;

            // 공기 저항으로 궤적이 짧아지지 않도록 점프 동안 감쇠를 0으로 고정
            savedLinearDamping = golem.rigid.linearDamping;
            golem.rigid.linearDamping = 0f;

            Vector2 startPos = golem.transform.position;
            Vector2 playerPos = golem.player.position; // 시전 당시 플레이어 위치 고정

            // 플레이어 방향으로 시선 전환
            golem.moveDirection = playerPos.x >= startPos.x ? 1 : -1;
            golem.flip(golem.moveDirection);

            LaunchToTarget(startPos, playerPos);
        }

        public override void stateUpdate()
        {
            if (hasLanded) return;

            elapsedTime += Time.deltaTime;
            golem.isAnimeEnd = false; // 공중에서 애니메이션 이벤트로 점프가 끊기는 현상 차단

            // 바닥 마찰 등으로 수평 속도가 깎이지 않도록 체공 중 X 속도 유지
            golem.rigid.linearVelocity = new Vector2(targetVelocityX, golem.rigid.linearVelocity.y);

            if (!golem.isGround)
            {
                hasLeftGround = true;
            }

            // 1) 최소 이륙 시간 이후 하강 중 바닥 감지
            bool landedOnGround = elapsedTime > minAirTime && hasLeftGround && golem.isGround && golem.rigid.linearVelocity.y <= 0.05f;
            // 2) 예상 체공 시간 도달 후 수직 속도가 0이 되었을 때 (groundCheck가 낭떠러지에 걸린 경우 대비)
            bool fallbackLanded = elapsedTime >= expectedFlightTime && Mathf.Abs(golem.rigid.linearVelocity.y) < 0.1f;
            // 3) 예외 상황 무한 체공 방지
            bool timeoutLanded = elapsedTime > expectedFlightTime + 1.0f;

            if (landedOnGround || fallbackLanded || timeoutLanded)
            {
                OnLand();
            }
        }

        public override void stateExit()
        {
            golem.isJumping = false;
            golem.rigid.linearDamping = savedLinearDamping;
            golem.rigid.linearVelocity = new Vector2(0f, golem.rigid.linearVelocity.y);

            if (golem.anim != null)
            {
                golem.anim.SetBool("Jump", false);
            }

            golem.isWait = false;
            golem.startPosition = golem.transform.position;
        }

        private void LaunchToTarget(Vector2 startPos, Vector2 playerPos)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y * golem.rigid.gravityScale);
            if (gravity <= 0.001f) gravity = 9.81f;

            // 골렘 중심(transform)과 발밑(groundCheck) 사이의 높이 차이 계산
            float feetOffset = golem.groundCheck != null
                ? Mathf.Max(0f, startPos.y - golem.groundCheck.position.y)
                : 0f;

            // 플레이어 X 위치의 바닥을 찾아 골렘이 실제로 착지할 중심 Y 좌표 산출
            RaycastHit2D hit = Physics2D.Raycast(
                new Vector2(playerPos.x, playerPos.y + 0.5f),
                Vector2.down,
                20f,
                LayerMask.GetMask("Ground")
            );
            float landY = hit.collider != null ? hit.point.y + feetOffset : startPos.y;

            // 시전 당시 플레이어보다 4칸 위를 정점으로 하되, 시작/착지 높이보다 항상 높도록 보장
            float apexY = Mathf.Max(
                playerPos.y + jumpHeightAbovePlayer,
                Mathf.Max(startPos.y, landY) + minArcOffset
            );

            float heightUp = apexY - startPos.y;
            float heightDown = apexY - landY;

            float velocityY = Mathf.Sqrt(2f * gravity * heightUp);
            float timeUp = velocityY / gravity;
            float timeDown = Mathf.Sqrt(2f * heightDown / gravity);
            expectedFlightTime = timeUp + timeDown;

            targetVelocityX = (playerPos.x - startPos.x) / expectedFlightTime;

            golem.rigid.linearVelocity = new Vector2(targetVelocityX, velocityY);
        }

        private void OnLand()
        {
            hasLanded = true;
            golem.rigid.linearVelocity = Vector2.zero;
            golem.isJumping = false;
            golem.isAnimeEnd = true;
        }
    }

    public class ThrowRockState : BaseState
    {
        private Golem golem;

        public ThrowRockState(Monster monster) : base(monster)
        {
            golem = (Golem)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            golem.isWait = true;
            golem.moveSpeed = 0;
            golem.moveDirection = golem.player.position.x > golem.transform.position.x ? 1 : -1; //플레이어 방향으로 회전
            golem.flip(golem.moveDirection);

            golem.isAnimeEnd = false;

            golem.anim.SetBool("Idle", false);
            golem.anim.SetBool("Run", false);
            golem.anim.SetBool("Throw", true);
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {

        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            golem.anim.SetBool("Throw", false);
            golem.isWait = false;
            golem.startPosition = golem.transform.position;
        }
    }

    public class SummonState : BaseState
    {
        private Golem golem;

        public SummonState(Monster monster) : base(monster)
        {
            golem = (Golem)monster;
        }

        public override void stateEnter() //상태에 들어갈 때 실행되는 코드
        {
            golem.isWait = true;
            golem.moveSpeed = 0;

            golem.isAnimeEnd = false;

            golem.anim.SetBool("Idle", false);
            golem.anim.SetBool("Run", false);
            golem.anim.SetBool("Summon", true);
        }

        public override void stateUpdate() //상태에 들어갈 때 계속 실행되는 코드
        {

        }

        public override void stateExit() //상태에 나갈 때 실행되는 코드
        {
            golem.anim.SetBool("Summon", false);
            golem.isWait = false;
            golem.startPosition = golem.transform.position;
        }
    }
}
