using UnityEngine;

public class Arrow : MonoBehaviour
{
    [HideInInspector] private float moveSpeed = 2f;
    private float arrowDirection = 0f;
    private Vector2 moveDirVector;                    //화살이 이동할 방향 벡터
    private Skeleton skeleton;

    protected Rigidbody2D rigid;

    private void Start()
    {
        rigid = GetComponent<Rigidbody2D>();
    }

    void Update()
    {

        rigid.linearVelocity = moveDirVector * moveSpeed;
    }

    public void setSpeed(float speed)
    {
        moveSpeed = speed;
    }
    public void setSkeleton(Skeleton owner)
    {
        skeleton = owner;
    }
    public void setDirection(float direction, Vector2 vector)
    {
        transform.rotation = Quaternion.Euler(0, 0, direction + 180f); //회전 적용

        if (vector.x < 0)
        {
            transform.localScale = new Vector3(1, 1, 1); // 왼쪽일 땐 Y축 반전
        }
        else
        {
            transform.localScale = new Vector3(1, -1, 1);  // 오른쪽일 땐 원상 복구
        }
        moveDirVector = vector;
    }

    void OnBecameInvisible()
    {
        if (skeleton != null)
        {
            skeleton.isAttack = false;
        }
        Destroy(gameObject);
    }
    
}
