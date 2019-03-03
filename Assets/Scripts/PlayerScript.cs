using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerScript : MonoBehaviour
{
    [SerializeField]
    private GameObject Gun;
    [SerializeField]
    private GameObject Sword;
    [SerializeField]
    private GameObject Shield;  //잘 안보이므로 그냥 방패 쓰고 무슨 방패인지는 텍스트로 띄운다.
    [SerializeField]
    private GameObject Electric; //공용으로 하나만 사용 (양방향 이어져 있으므로)
    [SerializeField]
    private Slider HP;
    [SerializeField]
    private Text ShieldText;
    [SerializeField]
    private Text MoneyText;
    private bool AnimationDone = false;
    private bool isDead = false;

    // Start is called before the first frame update
    void Start()
    {
        Gun.SetActive(false);
        Sword.SetActive(false);
        Shield.SetActive(false);
        Electric.SetActive(false);
        ShieldText.text = "";
    }

    public void SetMoneyText(int money)
    {
        MoneyText.text = money.ToString();
    }

    //Attack Animations
    public void Shot()
    {
        AnimationDone = false;
        Gun.SetActive(true);
        StartCoroutine("ShotAnim");
    }

    IEnumerator ShotAnim()
    {
        float time = 0f;
        while(time < 2.2f)
        {
            yield return new WaitForEndOfFrame();
            time += Time.deltaTime;
        }
        Gun.GetComponent<SimpleShoot>().Shoot();
        time = 0f;
        while(time < 1f)
        {
            yield return new WaitForEndOfFrame();
            time += Time.deltaTime;
        }
        Gun.SetActive(false);
        AnimationDone = true;
    }

    public void Slash()
    {
        AnimationDone = false;
        //애니메이션 직접 제작할 것
        StartCoroutine("SlashAnim");
    }

    IEnumerator SlashAnim()
    {
        float time = 0f;
        Sword.SetActive(true);
        if(this.CompareTag("Player"))
            GetComponent<Animation>().Play("Slash");
        else
            GetComponent<Animation>().Play("EnemySlash");
        while (time < 2.2f)
        {
            yield return new WaitForEndOfFrame();
            time += Time.deltaTime;
        }
        Sword.SetActive(false);
        AnimationDone = true;
    }

    public void ElecShoc()
    {
        AnimationDone = false;
        Electric.SetActive(true);
        StartCoroutine("ElecAnim");
    }

    IEnumerator ElecAnim()
    {
        float time = 0f;
        while(time < 3f)
        {
            yield return new WaitForEndOfFrame();
            time += Time.deltaTime;
        }
        Electric.SetActive(false);
        AnimationDone = true;
    }

    //방어
    public void Defence(float dmg)
    {
        AnimationDone = false;
        Shield.SetActive(true);
        StartCoroutine("DefenceAnim", dmg);
    }

    IEnumerator DefenceAnim(float dmg)
    {
        float time = 0f;
        while(time < 3f)
        {
            yield return new WaitForEndOfFrame();
            time += Time.deltaTime;
        }
        Shield.SetActive(false);
        ShieldText.text = "";
        AnimationDone = true;
        isDead = GetDamage(dmg);
    }

    public bool GetDamage(float dmg)
    {
        HP.value -= dmg;

        return HP.value <= 0; //true: dead
    }

    public bool isAnimDone()
    {
        return AnimationDone;
    }

    public bool IsDead()
    {
        return isDead;
    }

    public void SetShieldText(int shield)
    {
        switch ((InGameScript.Shield)shield)
        {
            case InGameScript.Shield.Iron:
                ShieldText.text = "IronShield";
                break;
            case InGameScript.Shield.Rubber:
                ShieldText.text = "RubberShield";
                break;
            case InGameScript.Shield.Wood:
                ShieldText.text = "WoodShield";
                break;
        }
    }
}
