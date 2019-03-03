using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class NetworkScript : MonoBehaviour
{
    //소켓접속(서버 + 클라이언트 통합되어 있다 추후 분리할것)

    private Socket ListeningSocket = null;  //서버의 리스닝 소켓
    private Socket ClientSocket = null; //클라이언트 접속용 소켓

    private PacketQueue sendQueue;  //패킷 송신용 큐
    private PacketQueue receiveQueue;   //패킷 수신용 큐

    private bool isServer = false; //서버 플래그
    private bool isConnected = false; //접속 플래그

    // ManualResetEvent instances signal completion.  
    private static ManualResetEvent connectDone = new ManualResetEvent(false);

    //이벤트 알림
    public delegate void EventHandler(NetEventState state);

    private EventHandler handler;

    //스레드
    protected bool threadLoop = false;
    protected Thread thread = null;
    private static int BUFFERSIZE = 1400;

    void Start()
    {
        sendQueue = new PacketQueue();
        receiveQueue = new PacketQueue();
    }

    //서버 대기 시작
    public bool StartServer(int port, int connectionNum)
    {
        Debug.Log("StartServer");

        try
        {
            //리스닝 소켓 생성
            ListeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //사용할 포트 번호 할당
            ListeningSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            //대기
            ListeningSocket.Listen(connectionNum);
        }
        catch
        {
            Debug.Log("StartServer Fail");
            return false;
        }

        isServer = true;

        return LaunchThread();
    }

    //대기 종료
    public void StopServer()
    {
        threadLoop = false;
        if(thread != null)
        {
            thread.Join();
            thread = null;
        }

        Disconnect();

        if(ListeningSocket != null)
        {
            ListeningSocket.Close();
            ListeningSocket = null;
        }

        isServer = false;
        Debug.Log("Server Stopped");
    }

    //접속 해제
    public void Disconnect()
    {
        isConnected = false;

        if(ClientSocket != null)
        {
            //소켓 닫기
            ClientSocket.Shutdown(SocketShutdown.Both);
            ClientSocket.Close();
            ClientSocket = null;

            //접속 종료 알리기
            if(handler != null)
            {
                NetEventState state = new NetEventState();
                state.type = NetEventType.Disconnect;
                state.result = NetEventResult.Success;
                handler(state);
            }
        }
    }

    //송신처리
    public int Send(byte[] data, int size)
    {
        if(sendQueue == null)
            return 0;

        return sendQueue.Enqueue(data, size);
    }

    //수신처리
    public int Receive(ref byte[] buffer, int size)
    {
        if (receiveQueue == null)
            return 0;

        return receiveQueue.Dequeue(ref buffer, size);
    }

    //이벤트 등록, 해제
    public void RegisterHandler(EventHandler newHandler)
    {
        handler += newHandler;
    }

    public void UnRegisterHandler(EventHandler delHandler)
    {
        handler -= delHandler;
    }

    //스레드 시작 함수
    bool LaunchThread()
    {
        try
        {
            //Distpatch용 thread 시작
            threadLoop = true;
            thread = new Thread(new ThreadStart(Dispatch));
            thread.Start();
        }
        catch
        {
            Debug.Log("cannot launch thread.");
            return false;
        }

        return true;
    }

    //스레드 측의 송수신 처리
    public void Dispatch()
    {
        Debug.Log("Dispatch Thread Started");

        while(threadLoop)
        {
            //클라이언트로 부터의 접속을 기다린다.
            AcceptClient();

            //클라이언트와의 송수신을 처리한다.
            if(ClientSocket != null && isConnected == true)
            {
                //송신처리
                DispatchSend();
                //수신처리
                DispatchReceive();
            }

            Thread.Sleep(5);    // 5/1000초 쉬고 반복 -> 0.005초에 한번씩 송수신
        }

        Debug.Log("Dispatch Thread Ended");
    }

    //클라이언트와 접속
    void AcceptClient()
    {
        if(ListeningSocket != null && ListeningSocket.Poll(0, SelectMode.SelectRead))   //poll(특정 이벤트에 대해서 소켓을 주시하는 시간(마이크로초 단위), 주시해야 할 행동)
        {
            //클라이언트의 접속 탐지됨
            ClientSocket = ListeningSocket.Accept();
            isConnected = true;
            if (handler != null)
            {
                NetEventState state = new NetEventState();
                state.type = NetEventType.Connect;
                state.result = NetEventResult.Success;
                handler(state);
            }
            Debug.Log("Connect from Client");
        }
    }

    //서버측 송신처리(스레드 작업)
    void DispatchSend()
    {
        try
        {
            if(ClientSocket.Poll(0, SelectMode.SelectWrite))
            {
                byte[] buffer = new byte[BUFFERSIZE];
                int sendSize = sendQueue.Dequeue(ref buffer, buffer.Length);
                while(sendSize > 0)
                {
                    ClientSocket.Send(buffer, sendSize, SocketFlags.None);
                    sendSize = sendQueue.Dequeue(ref buffer, buffer.Length);
                }
            }
        }
        catch
        {
            return;
        }
    }

    //서버측 수신처리(스레드 작업)
    void DispatchReceive()
    {
        try
        {
            while(ClientSocket.Poll(0, SelectMode.SelectRead))
            {
                byte[] buffer = new byte[BUFFERSIZE];
                int receiveSize = ClientSocket.Receive(buffer, buffer.Length, SocketFlags.None);
                if(receiveSize == 0)
                {
                    //끊기
                    Debug.Log("Disconnect receive from client");
                    Disconnect();
                }
                else if(receiveSize > 0)
                {
                    receiveQueue.Enqueue(buffer, receiveSize);
                }
            }
        }
        catch
        {
            return;
        }
    }

    //서버인지 확인
    public bool IsServer()
    {
        return isServer;
    }
    //접속 확인
    public bool IsConnected()
    {
        return isConnected;
    }

    //클라이언트 접속부
    public bool Connect(string address, int port)
    {
        Debug.Log("Client Tries Connect to Server");


        if (ListeningSocket != null) return false;  //리스닝소켓이 null이 아니면, 현재 기기가 서버 역할이란 뜻이므로

        bool ret = false;
        try
        {
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //ClientSocket.Blocking = false;  //블러킹 없에면 연결 설정하는데 시간이 필요한데 바로 리턴해버리기 때문에 연결 안되는 듯 함.
            ClientSocket.NoDelay = true;    //Nagle alg off

            IPAddress ipaddress = IPAddress.Parse(address);
            IPEndPoint remoteEP = new IPEndPoint(ipaddress, port);

            ClientSocket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), ClientSocket);
            //ClientSocket.Connect(address, port);
            ret = LaunchThread();
        }
        catch
        {
            ClientSocket = null;
        }

        if(handler != null)
        {
            //접속 결과 알림
            NetEventState state = new NetEventState();
            state.type = NetEventType.Connect;
            state.result = isConnected ? NetEventResult.Success : NetEventResult.Failure;
        }

        return isConnected;
    }

    private void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket client = (Socket)ar.AsyncState;

            // Complete the connection.  
            client.EndConnect(ar);
            isConnected = true;
            Debug.Log("Socket connected to " + client.RemoteEndPoint.ToString());

            // Signal that the connection has been made.  
            connectDone.Set();
        }
        catch (Exception e)
        {
            isConnected = false;
            Debug.Log(e.ToString());
        }
    }
}
