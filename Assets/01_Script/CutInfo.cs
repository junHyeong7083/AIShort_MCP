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
}
