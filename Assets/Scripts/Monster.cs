using UnityEngine;
using UnityEngine.UI;

public class Monster : MonoBehaviour
{
    [SerializeField] private Slider HPSlider;
    [SerializeField] protected float maxHP = 100f;
    public float currentHP;
    public float moveSpeed = 4f;      //이동속도
    public float damage = 10f;        //공격력

    public Transform player;
    [SerializeField] protected float recognizeRadius = 8f;          //몬스터가 플레이어를 인식하는 범위 반지름
    [SerializeField] private float attackRadius = 4f;               //몬스터가 플레이어를 공격하는 범위
    protected bool isRecognzed = false;                             //몬스터가 플레이어를 인식했는지 확인
    protected float distanceToPlayer;                                  //몬스터와 플레이어 사이의 거리
    protected enum state{idle, notice, attack}                            //몬스터의 공격 상태
    protected state curState;                                       //현재 몬스터의 공격 상태

    public Transform groundCheck;                                   // 발밑 위치를 정할 오브젝트
    private GameObject monsterAttackHitbox;                         //슬라임 공격 동작의 공격 범위 오브젝트


    protected bool isWait = false;                                     //몬스터가 가만히 대기하는 상태
    [SerializeField] protected float maxWaitTime = 2f;                 //몬스터가 가만히 대기하는 최대 시간(초)
    protected float curWaitTime = 0f;                                  //몬스터가 가만히 대기한 시간(초)

    protected Animator anim;                                // 애니메이션 파라미터 제어를 위한 변수
    

    protected virtual void Start()
    {
        currentHP = maxHP;
        anim = GetComponent<Animator>();

        if (HPSlider != null)
        {
            HPSlider.maxValue = maxHP;
            HPSlider.value = currentHP;
        }

        monsterAttackHitbox = GameObject.Find("MonsterAttackHitbox");
        monsterAttackHitbox.SetActive(false); //슬라임 공격 동작의 공격 범위 비활성화
    }

    protected virtual void Update()
    {
        //몬스터와 플레이어 거리 계산
        distanceToPlayer = Vector2.Distance(transform.position, player.position);

        //플레이어와의 거리가 인식 반지름 이하인지 확인
        isRecognzed = distanceToPlayer <= recognizeRadius;
        
        //플레이어 인식 여부에 따라 상태 전환
        if (isRecognzed)
        {
            if (distanceToPlayer <= attackRadius)                             //플레이어가 공격 범위 안에 있을 때 공격 범위 활성화
            {
                curState = state.attack;
            }
            else
            {
                anim.SetBool("Attack", false);      //공격 애니메이션 끔
                flip(player.position.x > transform.position.x ? 1 : -1);      //플레이어가 인식 범위에 들어선 순간에 플레이어를 바라봄
                curState = state.notice;
            }
        }
        else
        {
            curState = state.idle;
        }
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;

        if (HPSlider != null)
        {
            HPSlider.value = currentHP;
        }

        if (currentHP <= 0)
        {
            moveSpeed = 0f;
            if (anim != null)
            {
                anim.SetTrigger("Die");
            }
            Destroy(gameObject, 1.0f);

        }
    }

    public void flip(int direction)
    {
        transform.localScale = new Vector3(direction, 1, 1);
    }

    protected bool groundChecker(float radius)
    {
        // 핵심: 발밑에 가상의 원을 그려서 'Ground' 레이어와 닿아있는지 체크
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, radius, LayerMask.GetMask("Ground"));
        return isGrounded;
    }

    public void setAttackHitbox(int on)
    {
        bool isOn = on>0 ? true : false;
        monsterAttackHitbox.SetActive(isOn);
    }
}
