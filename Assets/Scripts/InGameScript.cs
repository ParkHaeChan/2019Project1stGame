using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class InGameScript : MonoBehaviour
{
    private Camera MainCamera;
    private NetworkScript networkScript;
    private ManagerScript Manager;

    [SerializeField]
    private GameObject SceneText;
    [SerializeField]
    private GameObject SelectWeaponPanel;
    [SerializeField]
    private GameObject SelectArmorPanel;
    [SerializeField]
    private GameObject BettingPanel;
    [SerializeField]
    private Slider BettingSlider;
    [SerializeField]
    private Text BettingCoinText;
    [SerializeField]
    private GameObject timerText;
    [SerializeField]
    private int PlayerMoney;
    [SerializeField]
    private int EnemyMoney;
    [SerializeField]
    private GameObject Player;
    [SerializeField]
    private GameObject Enemy;

    private const int MAXIMUMBETTING = 10;

    private short Turn; //(0:선턴 무기선택, 1:후턴 방어구선택)
    private bool GameStarted = false;
    private bool isDraw = false;
    private GameState CurrentState;
    private GameState LastState;
    private byte[] ReceiveData = new byte[3];
    private byte[] SendData = new byte[3];
    private bool SelectDone = false;

    public enum GameState
    {
        None = -1,
        Wait = 0,
        SelectWeapon,
        SelectArmor,
        Bettimg,
        Win,
        Lose,
        Draw,
        Ready,
        WaitAnimation,
    };

    public enum Weapon
    {
        Gun = 0, Sword, Electric,
    };

    public enum Shield
    {
        Iron = 0, Rubber, Wood,
    };

    float[,] BattleTable = new float[3, 3] { {0f, .5f, 1f}, {0.1f, .4f, .7f}, {1f, 0f, .3f} };

    // Start is called before the first frame update
    void Start()
    {
        GameObject ManagerObj = GameObject.Find("Manager");
        Manager = ManagerObj.GetComponent<ManagerScript>();
        networkScript = ManagerObj.GetComponentInChildren<NetworkScript>();
        BettingSlider.maxValue = PlayerMoney > MAXIMUMBETTING ? MAXIMUMBETTING : PlayerMoney;
        MainCamera = Camera.main;
        StartCoroutine("CameraZoomIn");
        if (Manager.IsServer())
        {
            SceneText.GetComponent<Text>().text = "Waiting Client...";
            InvokeRepeating("CheckConnection", 0f, 1f);
        }
        else
        {
            SceneText.GetComponent<Text>().text = "Game Start!";
            //클라이언트는 서버의 턴 선택 결과를 기다린다.
            CurrentState = GameState.Wait;
        }
        
    }

    //카메라 줌인
    IEnumerator CameraZoomIn()
    {
        while (MainCamera.fieldOfView > 30)
        {
            MainCamera.fieldOfView--;
            yield return new WaitForSeconds(0.03f);
        }
    }

    //연결 확인 (InvokeRepeat로 연결 될 때 까지 반복 호출 된다)
    void CheckConnection()
    {
        if (networkScript.IsConnected() && GameStarted == false)
        {
            //클라이언트와 연결 됨
            SceneText.GetComponent<Text>().text = "Game Start!";
            GameStarted = true;

            //랜덤으로 선/후 턴 결정 (0:선턴, 1:후턴)
            Turn = (short)Random.Range(0, 2);   //(0,2)로 해야 0~1사이로 나온다 앞이 시작이고, 뒤가 갯수라 보면 됨
            
            SendData[0] = (byte)Turn;

            //클라이언트에 결과 전송
            networkScript.Send(SendData, 1);

            //상태변경 (wait -> select...)
            CurrentState = Turn == 0 ? GameState.SelectWeapon : GameState.SelectArmor;

            CancelInvoke();
        }
    }

    void Update()
    {
        switch(CurrentState)
        {
            case GameState.Wait:
                Wait();
                break;
            case GameState.SelectWeapon:
                SelectWeapon();
                break;
            case GameState.SelectArmor:
                SelectArmor();
                break;
            case GameState.Bettimg:
                BettingFunc();
                break;
            case GameState.Win:
                Manager.ShowPopupText("You Win!", 10f);
                CurrentState = GameState.None;
                break;
            case GameState.Lose:
                Manager.ShowPopupText("You Lose!", 10f);
                CurrentState = GameState.None;
                break;
            case GameState.Draw:
                Manager.ShowPopupText("Draw Game!", 10f);
                CurrentState = GameState.None;
                break;
            case GameState.Ready:
                Ready();
                break;
            case GameState.WaitAnimation:
                SceneText.GetComponent<Text>().text = "Battle!";
                WaitForAnimation();
                break;
            default:  //None
                break;
        }
    }

    void Wait()
    {
        //wait for data
        int recvSize = networkScript.Receive(ref ReceiveData, ReceiveData.Length);
        SceneText.GetComponent<Text>().text = "Waiting Opposite";
        if (recvSize <= 0)
            return; //아직 데이터 오지 않음
        else
        {
            //수신한 정보를 변환
            if (recvSize == 1)   //턴 선택 정보
            {
                short Opposite = (short)ReceiveData[0];
                Turn = (short)((Opposite + 1) % 2);

                //상태변경 (wait -> select...)
                CurrentState = Turn == 0 ? GameState.SelectWeapon : GameState.SelectArmor;
            }
            else if(recvSize == 3)  //무기 및 방어구 선택 정보 + 배팅정보 + 상대방 잔금 정보(3byte)
            {
                Debug.Log(ReceiveData[0].ToString() + ReceiveData[1].ToString() + ReceiveData[2].ToString());
                //결과 확인
                VerifyBattle();
                
            }
            else
            {
                Manager.ShowPopupText("Receive Error! must be 1 or 3 byte, recvSize: " + recvSize.ToString(), 10f);
                Debug.Log(ReceiveData[0].ToString() +ReceiveData[1].ToString() + ReceiveData[2].ToString());
            }
        }
    }

    void SelectWeapon()
    {
        SelectDone = false;
        SceneText.GetComponent<Text>().text = "Select Weapon!";
        SelectWeaponPanel.SetActive(true);
        StartCoroutine("SetTimer",10f);
        LastState = CurrentState;
        CurrentState = GameState.None;
    }

    void SelectArmor()
    {
        SelectDone = false;
        SceneText.GetComponent<Text>().text = "Select Armor!";
        SelectArmorPanel.SetActive(true);
        StartCoroutine("SetTimer", 10f);
        LastState = CurrentState;
        CurrentState = GameState.None;
    }

    void BettingFunc()
    {
        SelectDone = false;
        SceneText.GetComponent<Text>().text = "Betting~~~";
        BettingPanel.SetActive(true);
        StartCoroutine("SetTimer", 20f);
        LastState = CurrentState;
        CurrentState = GameState.None;
    }

    IEnumerator SetTimer(float time)
    {
        timerText.SetActive(true);
        
        while(time > 0 && SelectDone == false)
        {
            timerText.GetComponent<Text>().text = time.ToString("#.##");    //소수점 둘째자리까지 반환
            yield return new WaitForSeconds(0.1f);
            time -= 0.1f;
        }
        timerText.SetActive(false);
        if(time <= 0 && SelectDone == false)
        {
            //-> random selection(weapon & armor)
            if (LastState == GameState.SelectArmor || LastState == GameState.SelectWeapon)
            {
                SendData[0] = (byte)Random.Range(0, 3);
                
                //turn-off panel
                SelectArmorPanel.SetActive(false);
                SelectWeaponPanel.SetActive(false);

                //다음 상태로 변경 (배팅)
                LastState = CurrentState;
                CurrentState = GameState.Bettimg;
            }
            //-> betting fail(슬라이더 위치만큼 배팅: 기본 1)
            else if (LastState == GameState.Bettimg)
            {
                //배팅 정보
                SendData[1] = (byte)BettingSlider.value;
                //코인 차감
                CalcCoin(SendData[1]);
                //잔액 정보(무승부 처리용)
                SendData[2] = PlayerMoney == 0 ? (byte)0 : (byte)1;
                //선택 정보 전송
                networkScript.Send(SendData, SendData.Length);

                BettingPanel.SetActive(false);
                //상태변경 상대방 선택 정보 수신
                LastState = CurrentState;
                CurrentState = GameState.Wait;
            }
        }
    }
    //무기 버튼 선택
    public void GunButton()
    {
        SendData[0] = (byte)Weapon.Gun;
        ItemSelectComplete();
    }
    public void SwordButton()
    {
        SendData[0] = (byte)Weapon.Sword;
        ItemSelectComplete();
    }
    public void ElectricButton()
    {
        SendData[0] = (byte)Weapon.Electric;
        ItemSelectComplete();
    }
    //방어구 버튼 선택
    public void IronButton()
    {
        SendData[0] = (byte)Shield.Iron;
        ItemSelectComplete();
    }
    public void RubberButton()
    {
        SendData[0] = (byte)Shield.Rubber;
        ItemSelectComplete();
    }
    public void WoodButton()
    {
        SendData[0] = (byte)Shield.Wood;
        ItemSelectComplete();
    }

    void ItemSelectComplete()
    {
        SelectDone = true;
        if (LastState == GameState.SelectArmor)
            SelectArmorPanel.SetActive(false);
        else if (LastState == GameState.SelectWeapon)
            SelectWeaponPanel.SetActive(false);

        LastState = CurrentState;   //None
        CurrentState = GameState.Bettimg;
        //타이머 종료(안하면 다음 단계 타이머랑 텍스트 겹쳐짐)
        StopCoroutine("SetTimer");
        timerText.SetActive(false);
    }

    //배팅완료
    public void BettingDone()
    {
        SelectDone = true;
        //타이머 끄기
        StopCoroutine("SetTimer");
        timerText.SetActive(false);
        //현재는 1~10까지 배팅하므로 1byte로 가능하나 더 크게 할 경우 늘려야 함.
        SendData[1] = (byte)BettingSlider.value;
        //코인 차감
        CalcCoin(SendData[1]);
        //잔액 정보(무승부 처리용)
        SendData[2] = PlayerMoney == 0 ? (byte)0 : (byte)1;
        //선택 정보 전송
        networkScript.Send(SendData, SendData.Length);

        BettingPanel.SetActive(false);

        //상태변경 상대방 선택 정보 수신
        LastState = CurrentState;
        CurrentState = GameState.Wait;
    }

    //코인 차감
    void CalcCoin(byte bet)
    {
        //(DB처리로 나중에 변경 할 것)
        PlayerMoney -= bet;
        Player.GetComponent<PlayerScript>().SetMoneyText(PlayerMoney);
        if (PlayerMoney < 0)
        {
            //에러 발생...
            Manager.ShowPopupText("ERROR PLAYER MONEY < 0", 10f);
        }
    }

    //배팅슬라이더 OnValueChange
    public void SetBettingCoinText()
    {
        int betMoney = (int)BettingSlider.value;
        BettingCoinText.text = "Coin: " + betMoney.ToString();
    }

    //결과 확인
    void VerifyBattle()
    {
        isDraw = SendData[1] == ReceiveData[1] ? true : false;
        //상대방 잔액 계산
        EnemyMoney -= ReceiveData[1];
        Enemy.GetComponent<PlayerScript>().SetMoneyText(EnemyMoney);


        //비기지 않았을 경우 데미지 계산
        if (!isDraw)
        {
            bool isAttacker = SendData[1] > ReceiveData[1];
            int myitem, oppositeitem;
            float dmg;
            if (isAttacker)
            {//공격
                if (Turn == 0)
                {//내가 선택한 무기 장착
                    myitem = (int)SendData[0];
                    oppositeitem = (int)ReceiveData[0];
                }
                else
                {//상태가 선택한 무기 장착
                    myitem = (int)ReceiveData[0];
                    oppositeitem = (int)SendData[0];
                }
                dmg = CalcDamage(myitem, oppositeitem);

                //피격 애니메이션...
                AttackAnimation(Player, myitem);
                DefenceAnimation(Enemy, dmg, oppositeitem);
            }
            else
            {//방어
                if (Turn == 0)
                {//상대가 선택한 방어구 장착
                    myitem = (int)ReceiveData[0];
                    oppositeitem = (int)SendData[0];
                }
                else
                {//내가 선택한 방어구 장착
                    myitem = (int)SendData[0];
                    oppositeitem = (int)ReceiveData[0];
                }
                dmg = CalcDamage(oppositeitem, myitem);

                //피격 애니메이션...
                AttackAnimation(Enemy, oppositeitem);
                DefenceAnimation(Player, dmg, myitem);
            }
        }

        //턴교체하고 다음판 준비
        Turn = (short)((Turn + 1) % 2);
        LastState = CurrentState;
        CurrentState = GameState.Ready;
    }

    void ClearDataBuffer()
    {
        for(int i=0; i<SendData.Length; ++i)
        {
            SendData[i] = 0;
            ReceiveData[i] = 0;
        }
    }

    float CalcDamage(int weapon, int shield)
    {
        float damagePercente = BattleTable[weapon,shield];
        float weapondamage = -1f;
        //무기 공격력
        switch((Weapon)weapon)
        {
            case Weapon.Gun:
                weapondamage = 100f;
                break;
            case Weapon.Sword:
                weapondamage = 200f;
                break;
            case Weapon.Electric:
                weapondamage = 60f;
                break;
        }

        return weapondamage * damagePercente;
    }

    //다음판 시작전 준비
    void Ready()
    {
        bool oppositeMoneyZero = ReceiveData[2] == 0;

        //내 돈이 0이면
        if (PlayerMoney <= 0)
        {
            //상대방 돈이 0일 경우 무승부
            if (oppositeMoneyZero)
            {
                LastState = CurrentState;
                CurrentState = GameState.Draw;
                return;
            }
            else
            {//아닐경우 패배
                LastState = CurrentState;
                CurrentState = GameState.Lose;
                return;
            }
        }
        else
        {//내돈이 0이 아닌데 상대방 돈이 0이면 승
            if (oppositeMoneyZero)
            {
                LastState = CurrentState;
                CurrentState = GameState.Win;
                return;
            }
        }
        //데이터 버퍼 초기화
        ClearDataBuffer();

        //최대 배팅액 재조정
        BettingSlider.maxValue = PlayerMoney > MAXIMUMBETTING ? MAXIMUMBETTING : PlayerMoney;

        //Animation 기다기리
        if (!isDraw)
        {
            LastState = CurrentState;
            CurrentState = GameState.WaitAnimation;
        }
        else
        {//같은 배팅액일 경우 애니메이션 없이 3초 후 다음 라운드
            Manager.ShowPopupText("Draw! Same Betting!", 3f);
            Invoke("SelectItemState", 3f);
            LastState = CurrentState;
            CurrentState = GameState.None;
        }
    }

    void SelectItemState()
    {
        //상태변경 (Ready -> Select)
        LastState = CurrentState;
        CurrentState = Turn == 0 ? GameState.SelectWeapon : GameState.SelectArmor;
    }

    void WaitForAnimation()
    {
        //둘 다 애니메이션 끝났으면 다음 상태로 진행
        if (Player.GetComponent<PlayerScript>().isAnimDone() && Enemy.GetComponent<PlayerScript>().isAnimDone())
        {
            if (Player.GetComponent<PlayerScript>().IsDead())
            {//상태변경 (Ready -> Lose)
                LastState = CurrentState;
                CurrentState = GameState.Lose;
            }
            else if (Enemy.GetComponent<PlayerScript>().IsDead())
            {//상태변경 (Ready -> Win)
                LastState = CurrentState;
                CurrentState = GameState.Win;
            }
            else
            {//상태변경 (Ready -> Select)
                LastState = CurrentState;
                CurrentState = Turn == 0 ? GameState.SelectWeapon : GameState.SelectArmor;
            }
        }
    }

    void AttackAnimation(GameObject attacker, int weapon)
    {
        PlayerScript AttackAction = attacker.GetComponent<PlayerScript>();
        switch ((Weapon)weapon)
        {
            case Weapon.Gun:
                AttackAction.Shot();
                break;
            case Weapon.Sword:
                AttackAction.Slash();
                break;
            case Weapon.Electric:
                AttackAction.ElecShoc();
                break;
        }
    }

    void DefenceAnimation(GameObject Defencer, float dmg, int shield)
    {
        Defencer.GetComponent<PlayerScript>().Defence(dmg);
        Defencer.GetComponent<PlayerScript>().SetShieldText(shield);
    }
}
