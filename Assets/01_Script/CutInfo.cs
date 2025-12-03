using System;

[Serializable]
public class CutList
{
    public CutInfo[] cuts;
}

[Serializable]
public class CutInfo
{
    public int index;                 // 컷 번호 (1, 2, 3 ...)

    public string sceneDescriptionKo;

    public string cameraStyle;
    public string lightingStyle;
    public string movementSpeed;
    public string movementType;
    public string aestheticStyle;
    public string textStyle;

    // 2차 가공 결과
    public string koreanShot;
    public string englishPrompt;

    // @char 캐릭터 정보
    public string[] characterNames;      // 이 컷에 등장하는 캐릭터 이름들
    public string characterImagePath;    // 메인 캐릭터 이미지 로컬 경로 (base64 인코딩용)
}
