using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ReadySceneScript : MonoBehaviour
{
    [SerializeField]
    private GameObject MatchingPopUpPanel;
    [SerializeField]
    private Text TimeText;
    [SerializeField]
    private InputField inputField;

    private Mode currentMode;
    private NetworkScript networkscript = null;
    private string serverAddress;
    private const int port = 50505;
    private ManagerScript PopupMenu;

    enum Mode
    {
        None = 0,
        WaitForMatch,
        Connection,
        Error,
    };
    
    void Awake()
    {
        //Manager객체를 계속 유지하도록 만들기
        GameObject Manager = GameObject.Find("Manager");
        networkscript = Manager.GetComponentInChildren<NetworkScript>();
        PopupMenu = Manager.GetComponentInChildren<ManagerScript>();

        //서버 주소(일단 하드 코딩해놓고 실제 서버 사용시엔 DNS로 알아오는 방식으로 적용할 것)
        serverAddress = "121.181.137.193";  //고정 ip가 아니기 때문에 바뀔 수 있다.
        /*  DNS로 알아오는 방식
        // 호스트명을 가져옵니다.
		string hostname = Dns.GetHostName();
		// 호스트명에서 IP주소를 가져옵니다.
		IPAddress[] adrList = Dns.GetHostAddresses(hostname);
		serverAddress = adrList[0].ToString();
        */

        currentMode = Mode.None;
    }

    public void ChangeIP( ) //End Edit 이벤트로 호출
    {
        serverAddress = inputField.text;
    }

    public void StartAsClient()
    {
        MatchingPopUpPanel.SetActive(true);
        currentMode = Mode.WaitForMatch;
        PopupMenu.ShowPopupText("Wait For Match", 3f);

        //연결 시도
        Debug.Log(serverAddress);
        bool ret = networkscript.Connect(serverAddress, port);

        //비동기 연결 시그널이 올 때까지 대기
        StartCoroutine("SetTimer");
    }

    IEnumerator SetTimer()
    {
        int time = 0;
        while (!networkscript.IsConnected() && time <= 5)
        {
            TimeText.text = time.ToString();
            time++;
            yield return new WaitForSeconds(1f);
        }
        if(networkscript.IsConnected())//연결 성공, 다음 씬으로 이동
        {
            SceneManager.LoadScene("InGameScene", LoadSceneMode.Single);
        }
        else //시간초과 서버 연결 실패
        {
            PopupMenu.ShowPopupText("Server is not ready to play", 5f);
            CancelMatching();
        }
    }

    public void StartAsServer()
    {
        //PC만 서버로 활동
        if(Application.platform == RuntimePlatform.Android)
        {
            PopupMenu.ShowPopupText("Phone Can't Host Server!", 3f);
            return;
        }

        //서버 가동 후 다음씬으로 이동
        bool ret = networkscript.StartServer(port, 1);
        SceneManager.LoadScene("InGameScene", LoadSceneMode.Single);
    }

    public void CancelMatching()
    {
        TimeText.text = "0";
        StopCoroutine("SetTimer");
        MatchingPopUpPanel.SetActive(false);
        currentMode = Mode.None;
    }
}
