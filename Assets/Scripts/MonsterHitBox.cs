using UnityEngine;

public class MonsterHitBox : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerStatus playerStatus = collision.GetComponent<PlayerStatus>();

            Monster monster = GetComponentInParent<Monster>();

            //데미지 전달
            playerStatus.TakeDamage(monster.damage);
        }
    }
}
