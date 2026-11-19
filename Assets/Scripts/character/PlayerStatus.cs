using UnityEngine;
using System.Collections;

public class PlayerStatus : MonoBehaviour
{
    // ���� ����
    [SerializeField] private float maxHp = 100f;       // �ִ� ü��
    [SerializeField] private float currentHp;          //���� ü��

    [SerializeField] private float maxMp = 50f;        // �ִ� ����
    [SerializeField] private float currentMp;          // ���� ����

    //��� ����
    [SerializeField] private float defense = 5f;       // ����(������ ����)
    [SerializeField] private float invincibleTime = 0.5f; // �ǰ� ���� �ð�

    private bool isInvincible = false;                 // ���� ���� ���� ����
    private SpriteRenderer spriteRenderer;

    // �ٸ� ��ũ��Ʈ���� ������ �����ϰ� ���� �� �ֵ��� ������Ƽ(Property) ����
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float CurrentMp => currentMp;
    public float MaxMp => maxMp;
    public bool IsDead { get; private set; } = false;   // ��� ����

    void Start()
    {
        // ���� ���� �� ü�°� ������ �ִ�ġ�� �ʱ�ȭ
        currentHp = maxHp;
        currentMp = maxMp;

        // �ǰ� ���� ������ ���� ��������Ʈ ������ ������Ʈ
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // �÷��̾ �������� ���� �� ȣ���ϴ� �޼���
    public void TakeDamage(float damage)
    {
        // ����߰ų� ���� ���¶�� �������� ���� ����
        if (IsDead || isInvincible) return;

        // ������ ����� ���� ������ (�ּ� 1�� �������� �Ե��� ����)
        float finalDamage = Mathf.Max(damage - defense, 1f);

        currentHp -= finalDamage;
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp); // ü���� 0 �̸�, �ִ�ġ �ʰ����� �ʰ� ����

        Debug.Log($"�÷��̾ {finalDamage}�� �������� �Ծ����ϴ�. ���� ü��: {currentHp}");

        if (currentHp <= 0)
        {
            Die();
        }
        else
        {
            // ���� �ð� �� �ǰ� ���� ���� ����
            StartCoroutine(InvincibleCooldown());
        }
    }

    // �ǰ� �� ���� �ð� ���� ���·� ����� �����̴� ����
    private IEnumerator InvincibleCooldown()
    {
        // ���� ���·� ��ȯ
        isInvincible = true;

        // ���� �ð� ���� ĳ���͸� �����Ÿ��� ���� (�ǰ� �ð� ȿ��)
        float timer = 0f;
        while (timer < invincibleTime)
        {
            // ���İ��� 0.2�� 1.0 ���̷� ��ȯ
            float alpha = spriteRenderer.color.a == 1f ? 0.2f : 1f;
            spriteRenderer.color = new Color(1f, 1f, 1f, alpha);

            yield return new WaitForSeconds(0.1f);
            timer += 0.1f;
        }

        // ���� ���� �� ���� �������� ����
        spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        isInvincible = false;       // ���� ���� ����
    }

    // ���� / ���¹̳� �Һ� �޼���
    public bool ConsumeMp(float amount)
    {
        if (currentMp >= amount)
        {
            currentMp -= amount;
            return true; // �Һ� ����
        }

        Debug.Log("����/���¹̳��� �����մϴ�.");
        return false; // �Һ� ����
    }

    // ����  ȸ�� �޼��� (�����̳� �ڿ� ȸ����)
    public void RecoverMp(float amount)
    {
        currentMp += amount;
        currentMp = Mathf.Clamp(currentMp, 0f, maxMp);
    }

    // ü�� ȸ�� �޼��� (�����̳� ���ǿ�)
    public void RecoverHp(float amount)
    {
        if (IsDead) return;

        currentHp += amount;
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
    }

    // �÷��̾� ��� ó��
    private void Die()
    {
        IsDead = true;
        Debug.Log("�÷��̾ ����߽��ϴ�.");

        // TODO: �ִϸ����Ϳ��� ��� �ִϸ��̼� ����, ���� ��ũ��Ʈ ��Ȱ��ȭ ���� ������ ���⿡ �߰�
        // ��: GetComponent<PlayerMove>().enabled = false;
    }
}
