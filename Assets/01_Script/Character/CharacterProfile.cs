using System;

[Serializable]
public class CharacterProfile
{
    public string id;                // 고유 ID (GUID)
    public string name;              // @char 호출용 이름 (예: "민수")
    public string textProfile;       // 성격/말투/배경 설명 (GPT 프롬프트용)
    public string imagePath;         // 로컬 이미지 경로 (base64 인코딩용)
}

[Serializable]
public class CharacterProfileList
{
    public CharacterProfile[] characters;
}

/// <summary>
/// 배경 프리셋 (@back Name으로 호출)
/// </summary>
[Serializable]
public class BackgroundProfile
{
    public string id;                // 고유 ID (GUID)
    public string name;              // @back 호출용 이름 (예: "카페")
    public string description;       // 배경 설명 (GPT 프롬프트용)
    public string imagePath;         // 로컬 이미지 경로
}

[Serializable]
public class BackgroundProfileList
{
    public BackgroundProfile[] backgrounds;
}

/// <summary>
/// Runway referenceImages에 전달할 통합 참조 이미지 정보
/// </summary>
[Serializable]
public class ReferenceAsset
{
    public string name;              // 태그 이름 (@char/@back 뒤의 이름)
    public string imagePath;         // 로컬 이미지 경로
    public string type;              // "character" 또는 "background"
}
