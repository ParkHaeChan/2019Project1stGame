using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ManagerScript : MonoBehaviour
{
    private bool menuFlag = false;
    [SerializeField]
    private GameObject QuitMenuPrefab;
    [SerializeField]
    private GameObject PopupMessage;
    private NetworkScript networkScript;
    private bool isServer;

    void Start()
    {
        //종료 메뉴는 안보이는 상태로 시작
        QuitMenuPrefab.SetActive(false);
        networkScript = GetComponentInChildren<NetworkScript>();
        //팝업 텍스트도 안보이는 상태로 시작
        PopupMessage.SetActive(false);

        if (Application.platform == RuntimePlatform.Android)
            isServer = false;
        else
            isServer = true;
    }

    void Update()
    {
        //키입력 감지
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!menuFlag)
                ShowQuitMenu();
            else
                ResumeButton();
        }
    }

    public void ShowPopupText(string Text, float duration)
    {
        PopupMessage.GetComponent<Text>().text = Text;
        StartCoroutine("SetPopupText", duration);
    }

    IEnumerator SetPopupText(float duration)
    {
        PopupMessage.SetActive(true);
        yield return new WaitForSeconds(duration);
        PopupMessage.SetActive(false);
    }

    public void ShowQuitMenu()
    {
        //show Menu Panel
        menuFlag = true;
        QuitMenuPrefab.SetActive(true);
    }

    public void ExitButton()
    {
        if(SceneManager.GetActiveScene().name == "InGameScene")
        {
            if (isServer)
            {
                networkScript.StopServer();
            }
            else
            {   //client
                networkScript.Disconnect();
            }
        }
        Application.Quit();
    }

    public void ResumeButton()
    {
        //close Menu Panel
        menuFlag = false;
        QuitMenuPrefab.SetActive(false);
    }

    public bool IsServer()
    {
        return isServer;
    }
}
