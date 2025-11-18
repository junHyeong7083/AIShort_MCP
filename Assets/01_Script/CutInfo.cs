using System;

[Serializable]
public class CutList
{
    public CutInfo[] cuts;
}


public class CutInfo
{
    public int index;                 // 컷 번호
    public string shotType;           // 숏 타입(와이드, 클로즈업 등)
    public string sceneDescription;   // 장면 설명(한국어)
    public string characterPrompt;    // 인물 프롬프트(영어 위주)
    public string backgroundPrompt;   // 배경 프롬프트
    public string cameraPrompt;       // 카메라/무브/조명 등
    public string duration;           // 대략 몇 초짜리 컷인지
}
