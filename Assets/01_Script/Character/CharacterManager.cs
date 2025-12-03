using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 캐릭터/배경 프로필 저장/조회/관리
/// @char Name, @back Name 태그로 호출할 때 사용
/// Base64 방식: 로컬 이미지 경로만 저장, Runway 호출 시 base64로 인코딩
/// </summary>
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    [Header("Runtime Data")]
    public List<CharacterProfile> characters = new List<CharacterProfile>();
    public List<BackgroundProfile> backgrounds = new List<BackgroundProfile>();

    // 저장 경로
    private string CharacterSavePath => Path.Combine(Application.persistentDataPath, "characters.json");
    private string BackgroundSavePath => Path.Combine(Application.persistentDataPath, "backgrounds.json");

    // 이미지 저장 폴더 (앱 내부로 복사하여 보관)
    private string CharacterImageFolder => Path.Combine(Application.persistentDataPath, "CharacterImages");
    private string BackgroundImageFolder => Path.Combine(Application.persistentDataPath, "BackgroundImages");

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadCharacters();
            LoadBackgrounds();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ======================== 저장/로드 ========================

    public void SaveCharacters()
    {
        var list = new CharacterProfileList { characters = characters.ToArray() };
        string json = JsonUtility.ToJson(list, true);

        try
        {
            File.WriteAllText(CharacterSavePath, json);
            Debug.Log($"[CharacterManager] 캐릭터 저장 완료: {CharacterSavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterManager] 저장 실패: {e.Message}");
        }
    }

    public void LoadCharacters()
    {
        characters.Clear();

        if (!File.Exists(CharacterSavePath))
        {
            Debug.Log("[CharacterManager] 저장된 캐릭터 없음");
            return;
        }

        try
        {
            string json = File.ReadAllText(CharacterSavePath);
            var list = JsonUtility.FromJson<CharacterProfileList>(json);

            if (list != null && list.characters != null)
            {
                characters.AddRange(list.characters);
                Debug.Log($"[CharacterManager] {characters.Count}개 캐릭터 로드됨");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterManager] 로드 실패: {e.Message}");
        }
    }

    // ======================== 배경 저장/로드 ========================

    public void SaveBackgrounds()
    {
        var list = new BackgroundProfileList { backgrounds = backgrounds.ToArray() };
        string json = JsonUtility.ToJson(list, true);

        try
        {
            File.WriteAllText(BackgroundSavePath, json);
            Debug.Log($"[CharacterManager] 배경 저장 완료: {BackgroundSavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterManager] 배경 저장 실패: {e.Message}");
        }
    }

    public void LoadBackgrounds()
    {
        backgrounds.Clear();

        if (!File.Exists(BackgroundSavePath))
        {
            Debug.Log("[CharacterManager] 저장된 배경 없음");
            return;
        }

        try
        {
            string json = File.ReadAllText(BackgroundSavePath);
            var list = JsonUtility.FromJson<BackgroundProfileList>(json);

            if (list != null && list.backgrounds != null)
            {
                backgrounds.AddRange(list.backgrounds);
                Debug.Log($"[CharacterManager] {backgrounds.Count}개 배경 로드됨");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterManager] 배경 로드 실패: {e.Message}");
        }
    }

    // ======================== CRUD ========================

    /// <summary>
    /// 새 캐릭터 등록 (이미지를 앱 내부 폴더로 복사하여 보관)
    /// </summary>
    public CharacterProfile RegisterCharacter(string name, string textProfile, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogError("[CharacterManager] 캐릭터 이름이 비어있습니다.");
            return null;
        }

        // 중복 이름 체크
        if (GetCharacterByName(name) != null)
        {
            Debug.LogWarning($"[CharacterManager] 이미 존재하는 이름: {name}");
            return null;
        }

        // 이미지 파일 존재 확인
        if (!string.IsNullOrEmpty(imagePath) && !File.Exists(imagePath))
        {
            Debug.LogError($"[CharacterManager] 이미지 파일이 존재하지 않음: {imagePath}");
            return null;
        }

        // 이미지를 앱 내부 폴더로 복사
        string savedImagePath = CopyImageToStorage(imagePath, CharacterImageFolder, name);
        if (string.IsNullOrEmpty(savedImagePath))
        {
            Debug.LogError("[CharacterManager] 이미지 복사 실패");
            return null;
        }

        // 캐릭터 생성 (복사된 경로 사용)
        var profile = new CharacterProfile
        {
            id = Guid.NewGuid().ToString(),
            name = name,
            textProfile = textProfile ?? "",
            imagePath = savedImagePath  // 복사된 이미지 경로
        };

        characters.Add(profile);
        SaveCharacters();

        Debug.Log($"[CharacterManager] 캐릭터 등록 완료: {name} (저장된 이미지: {savedImagePath})");
        return profile;
    }

    /// <summary>
    /// 이름으로 캐릭터 조회 (@char Name 파싱 시 사용)
    /// </summary>
    public CharacterProfile GetCharacterByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return characters.Find(c =>
            string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 캐릭터 삭제
    /// </summary>
    public bool DeleteCharacter(string name)
    {
        var profile = GetCharacterByName(name);
        if (profile == null)
            return false;

        characters.Remove(profile);
        SaveCharacters();
        Debug.Log($"[CharacterManager] 캐릭터 삭제: {name}");
        return true;
    }

    /// <summary>
    /// 캐릭터 수정
    /// </summary>
    public CharacterProfile UpdateCharacter(string name, string newTextProfile, string newImagePath)
    {
        var profile = GetCharacterByName(name);
        if (profile == null)
        {
            Debug.LogError($"[CharacterManager] 캐릭터를 찾을 수 없음: {name}");
            return null;
        }

        // 텍스트 프로필 업데이트
        if (!string.IsNullOrEmpty(newTextProfile))
            profile.textProfile = newTextProfile;

        // 새 이미지 경로가 있고 파일이 존재하면 업데이트
        if (!string.IsNullOrEmpty(newImagePath) && File.Exists(newImagePath))
            profile.imagePath = newImagePath;

        SaveCharacters();
        Debug.Log($"[CharacterManager] 캐릭터 업데이트 완료: {profile.name}");
        return profile;
    }

    // ======================== Base64 인코딩 ========================

    /// <summary>
    /// 캐릭터 이미지를 Base64 Data URI로 변환
    /// Runway API의 promptImage에 직접 전달 가능
    /// </summary>
    public string GetCharacterImageAsBase64(string name)
    {
        var profile = GetCharacterByName(name);
        if (profile == null || string.IsNullOrEmpty(profile.imagePath))
            return null;

        return ImagePathToBase64DataUri(profile.imagePath);
    }

    /// <summary>
    /// 로컬 이미지 경로를 Base64 Data URI로 변환
    /// Runway API용으로 최적화: 이미지 리사이즈 + MIME 타입 보정
    /// </summary>
    public static string ImagePathToBase64DataUri(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            Debug.LogError($"[CharacterManager] 이미지 파일이 존재하지 않음: {imagePath}");
            return null;
        }

        try
        {
            // 원본 이미지 읽기
            byte[] originalBytes = File.ReadAllBytes(imagePath);

            // 텍스처로 로드하여 리사이즈 (Runway 최적화)
            Texture2D tex = new Texture2D(2, 2);
            if (!tex.LoadImage(originalBytes))
            {
                Debug.LogError($"[CharacterManager] 이미지 로드 실패: {imagePath}");
                return null;
            }

            // 최대 1024px로 리사이즈 (Runway 권장 크기)
            const int maxSize = 1024;
            int width = tex.width;
            int height = tex.height;

            if (width > maxSize || height > maxSize)
            {
                float scale = Mathf.Min((float)maxSize / width, (float)maxSize / height);
                int newWidth = Mathf.RoundToInt(width * scale);
                int newHeight = Mathf.RoundToInt(height * scale);

                RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
                Graphics.Blit(tex, rt);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;

                Texture2D resized = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
                resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
                resized.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                // 기존 텍스처 해제
                UnityEngine.Object.DestroyImmediate(tex);
                tex = resized;

                Debug.Log($"[CharacterManager] 이미지 리사이즈: {width}x{height} → {newWidth}x{newHeight}");
            }

            // JPEG으로 인코딩 (Runway는 image/jpg 선호)
            byte[] jpegBytes = tex.EncodeToJPG(85);  // 품질 85%
            UnityEngine.Object.DestroyImmediate(tex);

            string base64 = Convert.ToBase64String(jpegBytes);

            // Runway API는 image/jpg를 사용 (image/jpeg 아님!)
            string mimeType = "image/jpg";

            Debug.Log($"[CharacterManager] Base64 인코딩 완료: {imagePath}");
            Debug.Log($"[CharacterManager] MIME: {mimeType}, 크기: {base64.Length / 1024}KB, 시작: {base64.Substring(0, Math.Min(20, base64.Length))}");

            return $"data:{mimeType};base64,{base64}";
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterManager] Base64 인코딩 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 파일 헤더(매직 바이트)로 실제 이미지 MIME 타입 판단
    /// </summary>
    private static string DetectImageMimeType(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length < 4)
            return "image/png";

        // JPEG: FF D8 FF
        if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
        {
            Debug.Log("[CharacterManager] 매직 바이트: JPEG 감지");
            return "image/jpeg";
        }

        // PNG: 89 50 4E 47
        if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
        {
            Debug.Log("[CharacterManager] 매직 바이트: PNG 감지");
            return "image/png";
        }

        // GIF: 47 49 46 38
        if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x38)
            return "image/gif";

        // WebP: RIFF....WEBP
        if (imageBytes.Length >= 12 &&
            imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x46 &&
            imageBytes[8] == 0x57 && imageBytes[9] == 0x45 && imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
            return "image/webp";

        Debug.LogWarning("[CharacterManager] 알 수 없는 이미지 포맷, 기본값 PNG");
        return "image/png";
    }

    // ======================== @char 파싱 유틸 ========================

    /// <summary>
    /// 텍스트에서 @char 태그들을 찾아서 캐릭터 목록 반환
    /// 형식: @char 이름 (공백으로 구분)
    /// 예: "@char 민수가 카페에 들어간다" → ["민수"]
    /// </summary>
    public List<CharacterProfile> ParseCharacterTags(string text)
    {
        var result = new List<CharacterProfile>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        // @char 이름 패턴 찾기
        int idx = 0;
        while ((idx = text.IndexOf("@char ", idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            idx += 6; // "@char " 길이

            // 다음 공백 또는 문장 끝까지가 이름
            int endIdx = idx;
            while (endIdx < text.Length && !char.IsWhiteSpace(text[endIdx]))
                endIdx++;

            if (endIdx > idx)
            {
                string rawName = text.Substring(idx, endIdx - idx).Trim();
                // 한글 조사 제거
                string charName = StripKoreanParticles(rawName);
                var profile = GetCharacterByName(charName);

                if (profile != null && !result.Contains(profile))
                    result.Add(profile);
                else if (profile == null)
                    Debug.LogWarning($"[CharacterManager] 등록되지 않은 캐릭터: {charName} (원본: {rawName})");
            }

            idx = endIdx;
        }

        return result;
    }

    /// <summary>
    /// 한글 조사 목록 (긴 것부터 먼저 체크)
    /// </summary>
    private static readonly string[] KoreanParticles = {
        "에서", "에게", "으로", "처럼", "보다", "까지", "부터", "마저", "조차", "만큼",
        "과", "와", "을", "를", "이", "가", "은", "는", "로", "의", "에", "도", "만"
    };

    /// <summary>
    /// 단어 끝의 한글 조사 제거
    /// </summary>
    private string StripKoreanParticles(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        foreach (var particle in KoreanParticles)
        {
            if (word.EndsWith(particle))
            {
                string stripped = word.Substring(0, word.Length - particle.Length);
                if (!string.IsNullOrEmpty(stripped))
                    return stripped;
            }
        }

        return word;
    }

    /// <summary>
    /// 텍스트에서 @char 태그 제거 (프롬프트 정리용)
    /// @char 이름 → 이름 (태그만 제거)
    /// </summary>
    public string RemoveCharacterTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // @char 이름 → 이름 (태그만 제거하고 이름+조사는 유지)
        // 예: "@char 철수가 걸어간다" → "철수가 걸어간다"
        var result = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"@char\s+(\S+)",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return result.Trim();
    }

    /// <summary>
    /// 캐릭터 프로필들을 GPT 컨텍스트 문자열로 변환
    /// </summary>
    public string BuildCharacterContext(List<CharacterProfile> profiles)
    {
        if (profiles == null || profiles.Count == 0)
            return "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[등장인물 프로필]");

        foreach (var p in profiles)
        {
            sb.AppendLine($"- {p.name}: {p.textProfile}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 파싱된 배경 목록을 GPT에게 전달할 컨텍스트 문자열로 변환
    /// </summary>
    public string BuildBackgroundContext(List<BackgroundProfile> profiles)
    {
        if (profiles == null || profiles.Count == 0)
            return "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[배경/장소 설정]");

        foreach (var p in profiles)
        {
            sb.AppendLine($"- {p.name}: {p.description}");
        }

        return sb.ToString();
    }

    // ======================== 배경 CRUD ========================

    /// <summary>
    /// 새 배경 등록 (이미지를 앱 내부 폴더로 복사하여 보관)
    /// </summary>
    public BackgroundProfile RegisterBackground(string name, string description, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogError("[CharacterManager] 배경 이름이 비어있습니다.");
            return null;
        }

        if (GetBackgroundByName(name) != null)
        {
            Debug.LogWarning($"[CharacterManager] 이미 존재하는 배경: {name}");
            return null;
        }

        if (!string.IsNullOrEmpty(imagePath) && !File.Exists(imagePath))
        {
            Debug.LogError($"[CharacterManager] 배경 이미지 파일이 존재하지 않음: {imagePath}");
            return null;
        }

        // 이미지를 앱 내부 폴더로 복사
        string savedImagePath = CopyImageToStorage(imagePath, BackgroundImageFolder, name);
        if (string.IsNullOrEmpty(savedImagePath))
        {
            Debug.LogError("[CharacterManager] 배경 이미지 복사 실패");
            return null;
        }

        var profile = new BackgroundProfile
        {
            id = Guid.NewGuid().ToString(),
            name = name,
            description = description ?? "",
            imagePath = savedImagePath  // 복사된 이미지 경로
        };

        backgrounds.Add(profile);
        SaveBackgrounds();

        Debug.Log($"[CharacterManager] 배경 등록 완료: {name} (저장된 이미지: {savedImagePath})");
        return profile;
    }

    // ======================== 이미지 복사 유틸 ========================

    /// <summary>
    /// 이미지를 지정된 폴더로 복사하고 저장된 경로 반환
    /// </summary>
    private string CopyImageToStorage(string sourcePath, string targetFolder, string assetName)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return null;

        try
        {
            // 폴더 생성
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            // 파일명 생성: {이름}_{GUID}.{확장자}
            string extension = Path.GetExtension(sourcePath);
            string safeAssetName = MakeSafeFileName(assetName);
            string fileName = $"{safeAssetName}_{Guid.NewGuid().ToString().Substring(0, 8)}{extension}";
            string targetPath = Path.Combine(targetFolder, fileName);

            // 복사
            File.Copy(sourcePath, targetPath, true);
            Debug.Log($"[CharacterManager] 이미지 복사 완료: {sourcePath} → {targetPath}");

            return targetPath;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterManager] 이미지 복사 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 파일명에 사용할 수 없는 문자 제거
    /// </summary>
    private string MakeSafeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "asset";

        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
            name = name.Replace(c, '_');

        return name;
    }

    /// <summary>
    /// 이름으로 배경 조회
    /// </summary>
    public BackgroundProfile GetBackgroundByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return backgrounds.Find(b =>
            string.Equals(b.name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 배경 삭제
    /// </summary>
    public bool DeleteBackground(string name)
    {
        var profile = GetBackgroundByName(name);
        if (profile == null)
            return false;

        backgrounds.Remove(profile);
        SaveBackgrounds();
        Debug.Log($"[CharacterManager] 배경 삭제: {name}");
        return true;
    }

    // ======================== @back 파싱 ========================

    /// <summary>
    /// 텍스트에서 @back 태그들을 찾아서 배경 목록 반환
    /// 형식: @back 이름 (공백으로 구분)
    /// 예: "@back 공항에서 @char 민수가 들어간다" → ["공항"]
    /// </summary>
    public List<BackgroundProfile> ParseBackgroundTags(string text)
    {
        var result = new List<BackgroundProfile>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        int idx = 0;
        while ((idx = text.IndexOf("@back ", idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            idx += 6; // "@back " 길이

            // 다음 공백 또는 문장 끝까지가 이름
            int endIdx = idx;
            while (endIdx < text.Length && !char.IsWhiteSpace(text[endIdx]))
                endIdx++;

            if (endIdx > idx)
            {
                string rawName = text.Substring(idx, endIdx - idx).Trim();
                // 한글 조사 제거
                string bgName = StripKoreanParticles(rawName);
                var profile = GetBackgroundByName(bgName);

                if (profile != null && !result.Contains(profile))
                    result.Add(profile);
                else if (profile == null)
                    Debug.LogWarning($"[CharacterManager] 등록되지 않은 배경: {bgName} (원본: {rawName})");
            }

            idx = endIdx;
        }

        return result;
    }

    /// <summary>
    /// 텍스트에서 @back 태그 제거
    /// @back 이름 → 이름 (태그만 제거)
    /// </summary>
    public string RemoveBackgroundTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // @back 이름 → 이름 (태그만 제거하고 이름+조사는 유지)
        // 예: "@back 공항에서 만난다" → "공항에서 만난다"
        var result = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"@back\s+(\S+)",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return result.Trim();
    }

    /// <summary>
    /// 텍스트에서 모든 태그(@char, @back) 제거
    /// </summary>
    public string RemoveAllTags(string text)
    {
        string result = RemoveCharacterTags(text);
        result = RemoveBackgroundTags(result);
        return result.Trim();
    }

    // ======================== Runway referenceImages 빌드 ========================

    /// <summary>
    /// 프롬프트에서 @char, @back 태그를 파싱하여 Runway referenceImages용 데이터 반환
    /// 최대 3개까지 반환 (Runway API 제한)
    /// </summary>
    public List<ReferenceAsset> BuildReferenceAssets(string text)
    {
        var result = new List<ReferenceAsset>();

        // @back 파싱 (배경은 보통 1개)
        var backgrounds = ParseBackgroundTags(text);
        foreach (var bg in backgrounds)
        {
            if (result.Count >= 3) break;
            result.Add(new ReferenceAsset
            {
                name = bg.name,
                imagePath = bg.imagePath,
                type = "background"
            });
        }

        // @char 파싱
        var characters = ParseCharacterTags(text);
        foreach (var ch in characters)
        {
            if (result.Count >= 3) break;
            result.Add(new ReferenceAsset
            {
                name = ch.name,
                imagePath = ch.imagePath,
                type = "character"
            });
        }

        return result;
    }

    /// <summary>
    /// 프롬프트에 @char 이름 또는 @back 이름 태그가 있는지 확인
    /// </summary>
    public bool HasReferenceTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.IndexOf("@char ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("@back ", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
