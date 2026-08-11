using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Experimental.GraphView.GraphView;

public class Monster : MonoBehaviour
{
    [SerializeField] private Slider HPSlider;
    [SerializeField] protected float maxHP = 100f;
    public float currentHP;
    public float moveSpeed = 4f;      //이동속도
    public float damage = 10f;        //공격력

    public Transform player;
    [SerializeField] protected float recognizeRadius = 5f;          //몬스터가 플레이어를 인식하는 범위 반지름
    protected bool isRecognzed = false;                             //몬스터가 플레이어를 인식했는지 확인
    protected bool wasRecognized = false;                           //이전 프레임의 인식 상태 확인
    public float distanceToPlayer;                                  //몬스터와 플레이어 사이의 거리
    protected enum state{normal, attack}                            //몬스터의 공격 상태
    protected state curState;                                       //현재 몬스터의 공격 상태

    protected bool isWait = false;                                     //몬스터가 가만히 대기하는 상태
    [SerializeField] protected float maxWaitTime = 2f;                 //몬스터가 가만히 대기하는 최대 시간(초)
    protected float curWaitTime = 0f;                                  //몬스터가 가만히 대기한 시간(초)

    protected SpriteRenderer spriteRenderer;              // 좌우 방향 뒤집기용(flipx)

    protected virtual void Start()
    {
        currentHP = maxHP;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (HPSlider != null)
        {
            HPSlider.maxValue = maxHP;
            HPSlider.value = currentHP;
        }
    }

    protected virtual void Update()
    {
        //몬스터와 플레이어 거리 계산
        distanceToPlayer = Vector2.Distance(transform.position, player.position);

        //플레이어와의 거리가 인식 반지름 이하인지 확인
        isRecognzed = distanceToPlayer <= recognizeRadius;

        //플레이어가 인식 범위에 들어선 순간에 플레이어를 바라봄
        if (isRecognzed==true && wasRecognized==false)
        {
            if (player.position.x > transform.position.x)           //플레이어가 몬스터의 오른쪽에 있을 때
            {
                flip(1);
            }
            else if (player.position.x < transform.position.x)      //플레이어가 몬스터의 왼쪽에 있을 때
            {
                flip(-1);
            }
        }
        wasRecognized = isRecognzed;                //이전 프레임의 인식 상태 저장
        

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
            Destroy(this);
        }
    }

    public void flip(int direction)
    {
        spriteRenderer.flipX = direction <= 0;
    }

    protected bool groundChecker(Vector2 position, float radius)
    {
        // 핵심: 발밑에 가상의 원을 그려서 'Ground' 레이어와 닿아있는지 체크
        bool isGrounded = Physics2D.OverlapCircle(position, radius, LayerMask.GetMask("Ground"));
        return isGrounded;
    }
}
