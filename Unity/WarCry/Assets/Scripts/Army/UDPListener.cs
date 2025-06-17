using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPListener : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static UDPListener Instance { get; private set; }

    [Header("UDP Settings")]
    public int port = 12345;
    public bool startListeningOnStart = true;
    private UdpClient udpClient;
    private Thread listenerThread;
    private bool isListening = false;
    private Queue<string> messageQueue = new Queue<string>();
    private object queueLock = new object();

    void Awake()
    {
        // 싱글톤 패턴 구현
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("UDPListener 싱글톤 초기화 - DontDestroyOnLoad 적용됨");
    }

    void Start()
    {
        if (startListeningOnStart)
            StartListening();
    }

    void Update()
    {
        while (true)
        {
            string raw;
            lock (queueLock)
            {
                if (messageQueue.Count == 0) break;
                raw = messageQueue.Dequeue();
                Debug.Log($"[UDPListener] 처리할 메시지: {raw}"); // 처리 메시지 로그 추가
            }
            // 모든 CommandProcessor에게 전달
            var processors = FindObjectsOfType<CommandProcessor>();
            Debug.Log($"[UDPListener] 발견된 CommandProcessor 수: {processors.Length}"); // CommandProcessor 수 로그 추가
            foreach (var cp in processors)
            {
                cp.ProcessCommand(raw);
            }
        }
    }

    public void StartListening()
    {
        if (isListening) return;
        try
        {
            udpClient = new UdpClient(port);
            listenerThread = new Thread(ListenForMessages);
            listenerThread.IsBackground = true;
            listenerThread.Start();
            isListening = true;
            Debug.Log($"UDPListener started on port {port}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"UDPListener start error: {ex.Message}");
        }
    }

    private void ListenForMessages()
    {
        var remoteEP = new IPEndPoint(IPAddress.Any, port);
        while (isListening)
        {
            try
            {
                var data = udpClient.Receive(ref remoteEP);
                var msg = Encoding.UTF8.GetString(data);
                Debug.Log($"[UDPListener] 메시지 수신: {msg}"); // 수신 메시지 로그 추가
                lock (queueLock)
                    messageQueue.Enqueue(msg);
            }
            catch (Exception ex)
            {
                Debug.LogError($"UDPListener receive error: {ex.Message}");
            }
        }
    }

    void OnDestroy()
    {
        isListening = false;
        listenerThread?.Abort();
        udpClient?.Close();
    }
}