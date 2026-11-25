using System.Collections.Generic;
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
        private const float edgeCheckDelay = 0.1f; // [추가] 발판 끝 판정 유예 시간
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

            // [추가] 발판 끝(앞에 땅 없음)에 막혀 멈추면 걷기 모션을 끄고 추적 종료
            // 다음 setState()에서 isBlockedByEdge를 보고 원거리 공격(점프/던지기)을 선택
            // (방향 전환 직후 isGround가 FixedUpdate에서 갱신되기 전 오판정을 막기 위해 짧은 유예 시간)
            if (!golem.isGround && curWaitTime > edgeCheckDelay)
            {
                golem.anim.SetBool("Run", false);
                golem.moveSpeed = 0f;
                golem.isBlockedByEdge = true;
                golem.isAnimeEnd = true;
                return;
            }

            if (golem.curMoveDistance > golem.traceMoveDistance)
            {
                golem.isWait = true; //대기상태 갱신
                golem.curMoveDistance = 0f; // 이동 거리 초기화

                // [기존 코드]
                // if (golem.distanceToPlayer <= golem.attackRadius && !golem.isJumping) //현재 일반 공격 범위에 들었을때
                if (golem.distanceToPlayer <= golem.attackRadius && !golem.IsPlayerOnDifferentFloor() && !golem.isJumping) //[수정] 같은 층에서 일반 공격 범위에 들었을때
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

        // [추가] 한 방향 발판(PlatformEffector2D) 통과용
        private const float landingTolerance = 0.3f;    // 착지 높이보다 이만큼 위에서 발판 충돌을 다시 켬
        private const float pathAreaMargin = 1f;        // 경로 검사 영역 여유치
        private readonly List<Collider2D> ignoredPlatforms = new List<Collider2D>(); // 점프 동안 충돌을 끈 발판들
        private Collider2D[] golemColliders;            // 골렘 본체 콜라이더들
        private float landFeetY;                        // 목적지 바닥의 표면 Y (발 기준)
        private bool platformCollisionRestored;         // 발판 충돌을 다시 켰는지

        // [추가] 착지 애니메이션 대기용
        private const float landAnimTimeout = 4f;       // 애니메이션 이벤트가 오지 않을 때(화면 밖 등) 강제 종료 시간
        private float landedTime;                       // 착지 후 경과 시간

        // [추가] Golem_Jump 클립 구간 나누기 (클립 길이 0.2667초 기준 normalizedTime)
        // 0 ~ 0.083초: 도약 / 0.083 ~ 0.15초: 공중 자세 / 0.15초 ~ 끝: 착지
        private const float airPoseNormalized = 0.45f;     // 이 지점(약 0.12초, 공중 자세 프레임)에서 체공 중 정지
        private const float landStartNormalized = 0.5625f; // 착지 시 이 지점(0.15초, 착지 첫 프레임)부터 재생
        private bool airPoseHeld;                          // 공중 자세에서 멈췄는지

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
                // [기존 코드]
                // // [추가] 체공 중에는 점프 클립 첫 프레임에서 멈춰 있다가 착지할 때(OnLand) 재생
                // golem.anim.Play("Golem_Jump", 0, 0f);
                // golem.anim.speed = 0f;
                // [수정] 이륙 시 도약 구간부터 재생 → 공중 자세에서 멈춤(stateUpdate) → 착지 시 착지 구간 재생(OnLand)
                golem.anim.Play("Golem_Jump", 0, 0f);
                golem.anim.speed = 1f;
            }
            airPoseHeld = false; // [추가]

            landedTime = 0f; // [추가]
            golem.isAnimeEnd = false;
            golem.isJumping = true;
            golem.isWait = false;

            hasLeftGround = false;
            hasLanded = false;
            elapsedTime = 0f;
            platformCollisionRestored = false; // [추가]
            ignoredPlatforms.Clear();          // [추가]

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
            // [기존 코드]
            // if (hasLanded) return;
            // [수정] 착지 후에는 착지 애니메이션이 끝나기를 기다림 (보통은 클립의 animeClosing(1) 이벤트로 isAnimeEnd = true)
            if (hasLanded)
            {
                landedTime += Time.deltaTime;
                if (golem.anim != null)
                {
                    AnimatorStateInfo info = golem.anim.GetCurrentAnimatorStateInfo(0);
                    if (info.IsName("Golem_Jump") && info.normalizedTime >= 1f) { golem.isAnimeEnd = true; }
                }
                if (landedTime > landAnimTimeout) { golem.isAnimeEnd = true; }
                return;
            }

            elapsedTime += Time.deltaTime;

            // [추가] 도약 구간 재생이 끝나 공중 자세 프레임에 도달하면 착지할 때까지 정지
            if (!airPoseHeld && golem.anim != null)
            {
                AnimatorStateInfo airInfo = golem.anim.GetCurrentAnimatorStateInfo(0);
                if (airInfo.IsName("Golem_Jump") && airInfo.normalizedTime >= airPoseNormalized)
                {
                    golem.anim.speed = 0f;
                    airPoseHeld = true;
                }
            }

            // [기존 코드] Golem.setState()의 isJumping 가드로 대체
            // golem.isAnimeEnd = false; // 공중에서 애니메이션 이벤트로 점프가 끊기는 현상 차단

            // 바닥 마찰 등으로 수평 속도가 깎이지 않도록 체공 중 X 속도 유지
            golem.rigid.linearVelocity = new Vector2(targetVelocityX, golem.rigid.linearVelocity.y);

            if (!golem.isGround)
            {
                hasLeftGround = true;
            }

            // [추가] 하강 중 발이 목적지 높이 근처까지 내려오면 꺼두었던 발판 충돌을 다시 켬
            // 한 방향 발판은 위에서 내려올 때만 막으므로, 목적지보다 위에 있는 중간 발판은 이미 지나친 상태
            if (!platformCollisionRestored && elapsedTime > minAirTime && golem.rigid.linearVelocity.y <= 0f)
            {
                float feetY = golem.groundCheck != null ? golem.groundCheck.position.y : golem.transform.position.y;
                // 낙하 속도가 빠르면 한 프레임에 착지면을 지나칠 수 있으므로 속도만큼 여유치를 더함
                float tolerance = landingTolerance + Mathf.Abs(golem.rigid.linearVelocity.y) * Mathf.Max(Time.deltaTime, Time.fixedDeltaTime);
                if (feetY <= landFeetY + tolerance)
                {
                    RestorePlatformCollisions();
                }
            }

            // [기존 코드]
            // // 1) 최소 이륙 시간 이후 하강 중 바닥 감지
            // bool landedOnGround = elapsedTime > minAirTime && hasLeftGround && golem.isGround && golem.rigid.linearVelocity.y <= 0.05f;
            // // 2) 예상 체공 시간 도달 후 수직 속도가 0이 되었을 때 (groundCheck가 낭떠러지에 걸린 경우 대비)
            // bool fallbackLanded = elapsedTime >= expectedFlightTime && Mathf.Abs(golem.rigid.linearVelocity.y) < 0.1f;
            // // 3) 예외 상황 무한 체공 방지
            // bool timeoutLanded = elapsedTime > expectedFlightTime + 1.0f;

            // [수정] 발판 충돌을 다시 켠 뒤에만 착지로 인정
            // 1) 최소 이륙 시간 이후 하강 중 바닥 감지
            bool landedOnGround = platformCollisionRestored && elapsedTime > minAirTime && hasLeftGround && golem.isGround && golem.rigid.linearVelocity.y <= 0.05f;
            // 2) 예상 체공 시간 도달 후 수직 속도가 0이 되었을 때 (groundCheck가 낭떠러지에 걸린 경우 대비)
            bool fallbackLanded = platformCollisionRestored && elapsedTime >= expectedFlightTime && Mathf.Abs(golem.rigid.linearVelocity.y) < 0.1f;
            // 3) 예외 상황 무한 체공 방지 (발판 통과로 체공이 길어질 수 있어 여유 시간을 늘림)
            bool timeoutLanded = elapsedTime > expectedFlightTime + 2.0f;

            if (landedOnGround || fallbackLanded || timeoutLanded)
            {
                OnLand();
            }
        }

        public override void stateExit()
        {
            RestorePlatformCollisions(); // [추가] 어떤 이유로 나가든(착지, 타임아웃, 사망) 발판 충돌을 반드시 복구
            golem.isJumping = false;
            golem.rigid.linearDamping = savedLinearDamping;
            golem.rigid.linearVelocity = new Vector2(0f, golem.rigid.linearVelocity.y);

            if (golem.anim != null)
            {
                golem.anim.speed = 1f; // [추가] 점프 도중 상태가 바뀌어도(사망 등) 애니메이션 속도 복구
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
            landFeetY = landY - feetOffset; // [추가] 발 기준 착지면 높이

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

            // [추가] 현재 위치 ~ 목적지 사이의 한 방향 발판 충돌을 끔
            IgnorePlatformsOnPath(startPos.x, playerPos.x, Mathf.Min(startPos.y - feetOffset, landFeetY), apexY);
        }

        // [추가] 점프 경로를 감싸는 영역 안의 한 방향 발판(PlatformEffector2D 사용) 콜라이더와 골렘의 충돌을 끔
        // Boss 씬은 Ground(단단한 바닥)와 Platform(한 방향 발판)이 같은 레이어라 레이어 단위가 아닌 콜라이더 단위로 처리
        // 단단한 바닥과 벽은 건드리지 않음
        private void IgnorePlatformsOnPath(float fromX, float toX, float bottomY, float topY)
        {
            golemColliders = golem.GetComponentsInChildren<Collider2D>();

            Vector2 areaMin = new Vector2(Mathf.Min(fromX, toX) - pathAreaMargin, bottomY - pathAreaMargin);
            Vector2 areaMax = new Vector2(Mathf.Max(fromX, toX) + pathAreaMargin, topY + pathAreaMargin);
            Collider2D[] hits = Physics2D.OverlapAreaAll(areaMin, areaMax);

            foreach (Collider2D platform in hits)
            {
                if (platform == null || platform.isTrigger || !platform.usedByEffector) continue;
                if (platform.attachedRigidbody == golem.rigid) continue; // 골렘 자신 제외
                if (ignoredPlatforms.Contains(platform)) continue;

                foreach (Collider2D golemCol in golemColliders)
                {
                    if (golemCol == null || golemCol.isTrigger) continue; // 히트박스 등 트리거는 제외
                    Physics2D.IgnoreCollision(golemCol, platform, true);
                }
                ignoredPlatforms.Add(platform);
            }

            platformCollisionRestored = ignoredPlatforms.Count == 0;
        }

        // [추가] 꺼두었던 발판 충돌을 다시 켬
        private void RestorePlatformCollisions()
        {
            if (golemColliders != null)
            {
                foreach (Collider2D platform in ignoredPlatforms)
                {
                    if (platform == null) continue;
                    foreach (Collider2D golemCol in golemColliders)
                    {
                        if (golemCol == null || golemCol.isTrigger) continue;
                        Physics2D.IgnoreCollision(golemCol, platform, false);
                    }
                }
            }
            ignoredPlatforms.Clear();
            platformCollisionRestored = true;
        }

        private void OnLand()
        {
            RestorePlatformCollisions(); // [추가]
            hasLanded = true;
            golem.rigid.linearVelocity = Vector2.zero;
            golem.isJumping = false;
            // [기존 코드]
            // golem.isAnimeEnd = true;
            // [수정] 착지 순간 점프 애니메이션 재생, 애니메이션이 끝날 때 isAnimeEnd = true (stateUpdate 참고)
            landedTime = 0f;
            // [기존 코드]
            // if (golem.anim != null) { golem.anim.speed = 1f; }
            // [수정] 도약 구간이 끝났든 안 끝났든 착지 구간 첫 프레임부터 재생
            if (golem.anim != null)
            {
                golem.anim.Play("Golem_Jump", 0, landStartNormalized);
                golem.anim.speed = 1f;
            }
            airPoseHeld = true;
            // [추가] 착지 후 FixedUpdate의 이동 로직으로 미끄러지지 않도록 정지
            golem.moveSpeed = 0f;
            golem.isWait = true;
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
