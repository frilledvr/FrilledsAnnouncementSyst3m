using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Photon.Pun;

[DisallowMultipleComponent]
public class FrilledsLobbySummonReceiver : MonoBehaviour
{
    [Header("Backend")] public string backendUrl = "https://your-render-service.onrender.com";
    public float pollInterval = 3f;
    [Header("Spawn Position") ] public Transform spawnPoint;
    public Vector3 fallbackPosition = Vector3.zero;
    public Vector3 fallbackEuler = Vector3.zero;
    private string lastCommandId = "";
    private bool busy;

    void Start(){StartCoroutine(PollSummons());}
    IEnumerator PollSummons(){while(true){if(!busy && PhotonNetwork.IsConnected && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient){busy=true;UnityWebRequest req=UnityWebRequest.Get(backendUrl.TrimEnd('/')+"/summons");req.timeout=10;yield return req.SendWebRequest();if(req.result==UnityWebRequest.Result.Success){SummonResponse d=null;try{d=JsonUtility.FromJson<SummonResponse>(req.downloadHandler.text);}catch{} if(d!=null&&d.summon!=null&&d.summon.id!=lastCommandId){lastCommandId=d.summon.id;Spawn(d.summon.prefab);}}req.Dispose();busy=false;}yield return new WaitForSeconds(Mathf.Max(1,pollInterval));}}
    void Spawn(string prefab){Vector3 pos=spawnPoint?spawnPoint.position:fallbackPosition;Quaternion rot=spawnPoint?spawnPoint.rotation:Quaternion.Euler(fallbackEuler);PhotonNetwork.Instantiate(prefab,pos,rot);}
    [System.Serializable] class SummonResponse{public SummonData summon;}
    [System.Serializable] class SummonData{public string id;public string prefab;public long createdAt;public string sender;}
}
