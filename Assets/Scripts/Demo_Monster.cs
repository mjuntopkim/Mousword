using UnityEngine;
using UnityEngine.UI;

public class Demo_Monster : MonoBehaviour
{
    [SerializeField] private Slider HPSlider;

    [SerializeField] private float maxHP = 100f;
    private float currentHP;

    private void Start()
    {
        currentHP = maxHP;

        if(HPSlider != null)
        {
            HPSlider.maxValue = maxHP;
            HPSlider.value = currentHP;
        }
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;

        if(HPSlider != null)
        {
            HPSlider.value = currentHP;
        }
    }
}
