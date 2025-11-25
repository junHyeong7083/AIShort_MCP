using System.Collections;
using System.IO;
using System;
using UnityEngine;

public class RunwayImageToVideoFlow : MonoBehaviour
{
    [Header("Runway 호출 담당")]
    [SerializeField] private RunwayVideoGenerator runwayGenerator;

    [Header("로컬 이미지 경로 (선택된 파일 경로)")]
    [SerializeField] private string localImagePath;

    public void SetLocalImagePath(string path)
    {
        localImagePath = path;
        Debug.Log("[RunwayImageToVideoFlow] localImagePath 세팅: " + localImagePath);
    }

    public void OnClick_GenerateFromLocalImage()
    {
        StartCoroutine(Co_RunFlow());
    }

    private IEnumerator Co_RunFlow()
    {
        if (runwayGenerator == null)
        {
            Debug.LogError("[RunwayImageToVideoFlow] runwayGenerator가 비어있습니다.");
            yield break;
        }

        if (string.IsNullOrEmpty(localImagePath) || !File.Exists(localImagePath))
        {
            Debug.LogError("[RunwayImageToVideoFlow] localImagePath가 비었거나 파일이 존재하지 않습니다.");
            yield break;
        }

        Debug.Log("[RunwayImageToVideoFlow] 로컬 이미지 → data URI 변환 시작");

        string dataUri;
        try
        {
            byte[] bytes = File.ReadAllBytes(localImagePath);

            // 확장자에 따라 MIME 타입 간단히 추정
            string ext = Path.GetExtension(localImagePath).ToLowerInvariant();
            string mime = "image/png";
            if (ext == ".jpg" || ext == ".jpeg") mime = "image/jpeg";
            else if (ext == ".webp") mime = "image/webp";

            string base64 = Convert.ToBase64String(bytes);
            dataUri = $"data:{mime};base64,{base64}";
        }
        catch (Exception e)
        {
            Debug.LogError("[RunwayImageToVideoFlow] data URI 변환 중 예외: " + e.Message);
            yield break;
        }

        Debug.Log("[RunwayImageToVideoFlow] data URI 생성 완료, 길이=" + dataUri.Length);

        // 여기서 바로 Runway 호출
        runwayGenerator.GenerateFromImageUrl(dataUri);

        yield break;
    }
}
