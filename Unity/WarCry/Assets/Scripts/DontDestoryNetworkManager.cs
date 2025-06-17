using UnityEngine;

public class DontDestroyNetworkManager : MonoBehaviour
{
    void Awake()
    {
        // 이 오브젝트가 씬 변경 시에도 유지되도록 설정
        Debug.Log("NetworkManager DontDestroyOnLoad 설정됨");
        DontDestroyOnLoad(this.gameObject);
    }
}