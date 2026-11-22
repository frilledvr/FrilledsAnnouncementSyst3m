using System.Collections;using UnityEngine;using UnityEngine.Networking;using UnityEngine.UI;
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
using TMPro;
#endif
[DisallowMultipleComponent]
public class FrilledsAnnouncementSystemUI : MonoBehaviour
{
 [Header("Camera Follow (attach to camera or assign)")] public Transform targetCamera; public Vector3 offset=new Vector3(0,0,2); public Vector3 rotationOffset=Vector3.zero; [Min(0)] public float followSmoothness=8f; public bool faceCamera=true;
 [Header("Display") ] public Canvas worldCanvas; public RectTransform panel; public Text legacyText; public Font legacyFont; public int fontSize=24; public Color textColor=Color.white;
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
 public TMP_Text tmpText; public TMP_FontAsset tmpFont;
#endif
 public Color panelColor=new Color(.07f,.07f,.09f,.95f); public Image panelImage; public Vector2 panelSize=new Vector2(800,220); public float displaySeconds=8;
 [Header("Backend") ] public string backendUrl="http://localhost:3000"; public float pollSeconds=5; public bool pollOnStart=true;
 private string latest=""; private float nextPoll; private long lastSeenCount=-1;
 void Start(){if(!targetCamera&&Camera.main)targetCamera=Camera.main.transform;ApplyStyle();if(pollOnStart)StartCoroutine(Poll());}
 void LateUpdate(){if(!targetCamera)return;Vector3 desired=targetCamera.TransformPoint(offset);transform.position=Vector3.Lerp(transform.position,desired,1-Mathf.Exp(-followSmoothness*Time.deltaTime));Quaternion rot=targetCamera.rotation*Quaternion.Euler(rotationOffset);transform.rotation=Quaternion.Slerp(transform.rotation,faceCamera?rot:Quaternion.Euler(rotationOffset),1-Mathf.Exp(-followSmoothness*Time.deltaTime));}
 public void ApplyStyle(){if(panel){panel.sizeDelta=panelSize;}if(panelImage)panelImage.color=panelColor;if(legacyText){legacyText.font=legacyFont;legacyText.fontSize=fontSize;legacyText.color=textColor;} 
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
 if(tmpText){if(tmpFont)tmpText.font=tmpFont;tmpText.fontSize=fontSize;tmpText.color=textColor;}
#endif
 }
 IEnumerator Poll(){while(true){UnityWebRequest req=UnityWebRequest.Get(backendUrl.TrimEnd('/')+"/announcements");req.timeout=10;yield return req.SendWebRequest();if(req.result==UnityWebRequest.Result.Success){string body=req.downloadHandler.text;AnnouncementResponse data=null;try{data=JsonUtility.FromJson<AnnouncementResponse>(body);}catch{}if(data!=null&&data.items!=null&&data.items.Length>0){var item=data.items[0];if(item.createdAt!=lastSeenCount && item.message!=latest){lastSeenCount=item.createdAt;latest=item.message;Show(latest);}}}req.Dispose();yield return new WaitForSeconds(Mathf.Max(2,pollSeconds));}}
 void Show(string m){if(legacyText)legacyText.text=m;
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
 if(tmpText)tmpText.text=m;
#endif
 if(panel)panel.gameObject.SetActive(true);if(displaySeconds>0)StartCoroutine(HideLater());}
 IEnumerator HideLater(){yield return new WaitForSeconds(displaySeconds);if(panel)panel.gameObject.SetActive(false);if(legacyText)legacyText.text="";
#if TMP_PRESENT || TEXTMESHPRO_PRESENT
 if(tmpText)tmpText.text="";
#endif
 }
 [System.Serializable]class AnnouncementResponse{public AnnouncementItem[] items;}[System.Serializable]class AnnouncementItem{public string message,sender;public long createdAt;}
}
