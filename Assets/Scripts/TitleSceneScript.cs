using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class TitleSceneScript : MonoBehaviour, IPointerClickHandler
{
    private NetworkScript networkscript;
    private ManagerScript manager;

    void Start()
    {
        //Manager객체를 계속 유지하도록 만들기
        GameObject Manager = GameObject.Find("Manager");
        networkscript = Manager.GetComponentInChildren<NetworkScript>();
        manager = Manager.GetComponentInChildren<ManagerScript>();
        DontDestroyOnLoad(Manager);
    }

    public void OnPointerClick(PointerEventData pointEventData)
    {
        //현재 wifi나 네트워크가 꺼져 있는 경우
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            manager.ShowPopupText("Network(Wifi or Data) not Reachable", 10f);
            Invoke("Quit", 11f);
        }
        else
        {
            SceneManager.LoadScene("ReadyScene", LoadSceneMode.Single);
        }
    }

    void Quit()
    {
        Application.Quit();
    }
}
