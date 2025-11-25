using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class UploadResponseDto
{
    public string url; // FastAPI 서버에서 {"url": "..."} 로 주는 값
}

public class ImageUploader : MonoBehaviour
{
    [Header("Python 업로드 서버 엔드포인트")]
    [SerializeField]
    private string uploadEndpoint = "http://127.0.0.1:8001/upload";

    /// <summary>
    /// filePath의 이미지를 Python 서버에 업로드하고,
    /// 업로드된 이미지 URL을 onComplete 콜백으로 넘겨준다.
    /// 실패하면 null을 넘긴다.
    /// </summary>
    public IEnumerator UploadImage(string filePath, Action<string> onComplete)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.LogError($"[ImageUploader] 잘못된 경로: {filePath}");
            onComplete?.Invoke(null);
            yield break;
        }

        byte[] bytes = File.ReadAllBytes(filePath);

        WWWForm form = new WWWForm();
        // FastAPI upload_image(file: UploadFile = File(...)) 이므로 필드 이름은 반드시 "file"
        form.AddBinaryData("file", bytes, Path.GetFileName(filePath), "image/png");

        using (UnityWebRequest req = UnityWebRequest.Post(uploadEndpoint, form))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ImageUploader] 업로드 실패: {req.responseCode}, {req.error}\n{req.downloadHandler.text}");
                onComplete?.Invoke(null);
                yield break;
            }

            string json = req.downloadHandler.text;
            Debug.Log("[ImageUploader] 서버 응답: " + json);

            UploadResponseDto res = null;
            try
            {
                res = JsonUtility.FromJson<UploadResponseDto>(json);
            }
            catch (Exception e)
            {
                Debug.LogError("[ImageUploader] JSON 파싱 실패: " + e.Message);
                onComplete?.Invoke(null);
                yield break;
            }

            if (res == null || string.IsNullOrEmpty(res.url))
            {
                Debug.LogError("[ImageUploader] 응답에 url 필드가 없음");
                onComplete?.Invoke(null);
                yield break;
            }

            onComplete?.Invoke(res.url);
        }
    }
}
