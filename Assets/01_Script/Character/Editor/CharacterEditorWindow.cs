#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Unity Editor에서 캐릭터/배경을 등록/관리하는 윈도우
/// 메뉴: Window > AI Short > Asset Manager
/// </summary>
public class CharacterEditorWindow : EditorWindow
{
    // 탭 선택
    private int _selectedTab = 0;
    private readonly string[] _tabNames = { "캐릭터 (@char)", "배경 (@back)" };

    // 캐릭터 입력
    private string _charName = "";
    private string _textProfile = "";
    private string _charImagePath = "";
    private Texture2D _charPreviewTexture;

    // 배경 입력
    private string _bgName = "";
    private string _bgDescription = "";
    private string _bgImagePath = "";
    private Texture2D _bgPreviewTexture;

    private Vector2 _scrollPos;

    // 데이터
    private CharacterProfileList _characterList;
    private BackgroundProfileList _backgroundList;

    private string CharacterSavePath => Path.Combine(Application.persistentDataPath, "characters.json");
    private string BackgroundSavePath => Path.Combine(Application.persistentDataPath, "backgrounds.json");

    // 이미지 저장 폴더
    private string CharacterImageFolder => Path.Combine(Application.persistentDataPath, "CharacterImages");
    private string BackgroundImageFolder => Path.Combine(Application.persistentDataPath, "BackgroundImages");

    [MenuItem("AI Short/Asset Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<CharacterEditorWindow>("Asset Manager");
        window.minSize = new Vector2(450, 600);
    }

    void OnEnable()
    {
        LoadCharacters();
        LoadBackgrounds();
    }

    void OnGUI()
    {
        // 탭 선택
        _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
        EditorGUILayout.Space(10);

        if (_selectedTab == 0)
        {
            DrawCharacterTab();
        }
        else
        {
            DrawBackgroundTab();
        }
    }

    // ======================== 캐릭터 탭 ========================
    void DrawCharacterTab()
    {
        GUILayout.Label("@char 캐릭터 등록", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // === 캐릭터 등록 섹션 ===
        EditorGUILayout.BeginVertical("box");
        {
            GUILayout.Label("새 캐릭터 등록", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _charName = EditorGUILayout.TextField("캐릭터 이름", _charName);

            GUILayout.Label("텍스트 프로필 (GPT용)");
            _textProfile = EditorGUILayout.TextArea(_textProfile, GUILayout.Height(60));

            EditorGUILayout.BeginHorizontal();
            {
                _charImagePath = EditorGUILayout.TextField("이미지 경로", _charImagePath);
                if (GUILayout.Button("선택", GUILayout.Width(50)))
                {
                    string path = EditorUtility.OpenFilePanel("캐릭터 이미지 선택", "", "png,jpg,jpeg");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _charImagePath = path;
                        LoadPreviewTexture(path, ref _charPreviewTexture);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_charPreviewTexture != null)
            {
                GUILayout.Label("미리보기:");
                GUILayout.Label(_charPreviewTexture, GUILayout.Width(100), GUILayout.Height(100));
            }

            EditorGUILayout.Space(10);

            GUI.enabled = !string.IsNullOrWhiteSpace(_charName) && !string.IsNullOrWhiteSpace(_charImagePath);
            if (GUILayout.Button("캐릭터 등록", GUILayout.Height(30)))
            {
                RegisterCharacter();
            }
            GUI.enabled = true;
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(20);

        // === 등록된 캐릭터 목록 ===
        EditorGUILayout.BeginVertical("box");
        {
            GUILayout.Label("등록된 캐릭터 목록", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (_characterList == null || _characterList.characters == null || _characterList.characters.Length == 0)
            {
                GUILayout.Label("등록된 캐릭터가 없습니다.");
            }
            else
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));
                {
                    foreach (var c in _characterList.characters)
                    {
                        EditorGUILayout.BeginHorizontal("box");
                        {
                            EditorGUILayout.BeginVertical();
                            {
                                GUILayout.Label($"@char {c.name}", EditorStyles.boldLabel);
                                GUILayout.Label($"프로필: {(string.IsNullOrEmpty(c.textProfile) ? "(없음)" : c.textProfile)}", EditorStyles.wordWrappedLabel);
                                GUILayout.Label($"이미지: {c.imagePath}", EditorStyles.miniLabel);
                            }
                            EditorGUILayout.EndVertical();

                            if (GUILayout.Button("삭제", GUILayout.Width(50)))
                            {
                                if (EditorUtility.DisplayDialog("삭제 확인", $"'{c.name}' 캐릭터를 삭제하시겠습니까?", "삭제", "취소"))
                                {
                                    DeleteCharacter(c.name);
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(5);
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("새로고침"))
            {
                LoadCharacters();
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
        GUILayout.Label($"저장 위치: {CharacterSavePath}", EditorStyles.miniLabel);
    }

    // ======================== 배경 탭 ========================
    void DrawBackgroundTab()
    {
        GUILayout.Label("@back 배경 등록", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // === 배경 등록 섹션 ===
        EditorGUILayout.BeginVertical("box");
        {
            GUILayout.Label("새 배경 등록", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _bgName = EditorGUILayout.TextField("배경 이름", _bgName);

            GUILayout.Label("배경 설명 (GPT용)");
            _bgDescription = EditorGUILayout.TextArea(_bgDescription, GUILayout.Height(60));

            EditorGUILayout.BeginHorizontal();
            {
                _bgImagePath = EditorGUILayout.TextField("이미지 경로", _bgImagePath);
                if (GUILayout.Button("선택", GUILayout.Width(50)))
                {
                    string path = EditorUtility.OpenFilePanel("배경 이미지 선택", "", "png,jpg,jpeg");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _bgImagePath = path;
                        LoadPreviewTexture(path, ref _bgPreviewTexture);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_bgPreviewTexture != null)
            {
                GUILayout.Label("미리보기:");
                GUILayout.Label(_bgPreviewTexture, GUILayout.Width(150), GUILayout.Height(100));
            }

            EditorGUILayout.Space(10);

            GUI.enabled = !string.IsNullOrWhiteSpace(_bgName) && !string.IsNullOrWhiteSpace(_bgImagePath);
            if (GUILayout.Button("배경 등록", GUILayout.Height(30)))
            {
                RegisterBackground();
            }
            GUI.enabled = true;
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(20);

        // === 등록된 배경 목록 ===
        EditorGUILayout.BeginVertical("box");
        {
            GUILayout.Label("등록된 배경 목록", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (_backgroundList == null || _backgroundList.backgrounds == null || _backgroundList.backgrounds.Length == 0)
            {
                GUILayout.Label("등록된 배경이 없습니다.");
            }
            else
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));
                {
                    foreach (var b in _backgroundList.backgrounds)
                    {
                        EditorGUILayout.BeginHorizontal("box");
                        {
                            EditorGUILayout.BeginVertical();
                            {
                                GUILayout.Label($"@back {b.name}", EditorStyles.boldLabel);
                                GUILayout.Label($"설명: {(string.IsNullOrEmpty(b.description) ? "(없음)" : b.description)}", EditorStyles.wordWrappedLabel);
                                GUILayout.Label($"이미지: {b.imagePath}", EditorStyles.miniLabel);
                            }
                            EditorGUILayout.EndVertical();

                            if (GUILayout.Button("삭제", GUILayout.Width(50)))
                            {
                                if (EditorUtility.DisplayDialog("삭제 확인", $"'{b.name}' 배경을 삭제하시겠습니까?", "삭제", "취소"))
                                {
                                    DeleteBackground(b.name);
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(5);
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("새로고침"))
            {
                LoadBackgrounds();
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
        GUILayout.Label($"저장 위치: {BackgroundSavePath}", EditorStyles.miniLabel);
    }

    // ======================== 공용 메서드 ========================
    void LoadPreviewTexture(string path, ref Texture2D texture)
    {
        if (File.Exists(path))
        {
            byte[] bytes = File.ReadAllBytes(path);
            texture = new Texture2D(2, 2);
            texture.LoadImage(bytes);
        }
    }

    // ======================== 캐릭터 로드/저장 ========================
    void LoadCharacters()
    {
        _characterList = null;

        if (File.Exists(CharacterSavePath))
        {
            try
            {
                string json = File.ReadAllText(CharacterSavePath);
                _characterList = JsonUtility.FromJson<CharacterProfileList>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"캐릭터 로드 실패: {e.Message}");
            }
        }

        if (_characterList == null)
            _characterList = new CharacterProfileList { characters = new CharacterProfile[0] };
    }

    void SaveCharacters()
    {
        string json = JsonUtility.ToJson(_characterList, true);
        File.WriteAllText(CharacterSavePath, json);
        Debug.Log($"캐릭터 저장 완료: {CharacterSavePath}");
    }

    void RegisterCharacter()
    {
        if (_characterList.characters != null)
        {
            foreach (var c in _characterList.characters)
            {
                if (c.name.Equals(_charName, System.StringComparison.OrdinalIgnoreCase))
                {
                    EditorUtility.DisplayDialog("오류", $"'{_charName}'은(는) 이미 등록된 이름입니다.", "확인");
                    return;
                }
            }
        }

        if (!File.Exists(_charImagePath))
        {
            EditorUtility.DisplayDialog("오류", "이미지 파일이 존재하지 않습니다.", "확인");
            return;
        }

        // 이미지를 앱 내부 폴더로 복사
        string savedImagePath = CopyImageToStorage(_charImagePath, CharacterImageFolder, _charName);
        if (string.IsNullOrEmpty(savedImagePath))
        {
            EditorUtility.DisplayDialog("오류", "이미지 복사에 실패했습니다.", "확인");
            return;
        }

        var newChar = new CharacterProfile
        {
            id = System.Guid.NewGuid().ToString(),
            name = _charName,
            textProfile = _textProfile,
            imagePath = savedImagePath  // 복사된 경로 사용
        };

        var list = new System.Collections.Generic.List<CharacterProfile>();
        if (_characterList.characters != null)
            list.AddRange(_characterList.characters);
        list.Add(newChar);
        _characterList.characters = list.ToArray();

        SaveCharacters();

        EditorUtility.DisplayDialog("완료", $"'{_charName}' 캐릭터가 등록되었습니다.\n\n사용법: @char {_charName}\n예: @char {_charName}가 걸어간다\n\n이미지가 앱 내부에 복사되었습니다.", "확인");

        _charName = "";
        _textProfile = "";
        _charImagePath = "";
        _charPreviewTexture = null;
    }

    void DeleteCharacter(string name)
    {
        var list = new System.Collections.Generic.List<CharacterProfile>();
        if (_characterList.characters != null)
        {
            foreach (var c in _characterList.characters)
            {
                if (!c.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    list.Add(c);
            }
        }
        _characterList.characters = list.ToArray();
        SaveCharacters();
    }

    // ======================== 배경 로드/저장 ========================
    void LoadBackgrounds()
    {
        _backgroundList = null;

        if (File.Exists(BackgroundSavePath))
        {
            try
            {
                string json = File.ReadAllText(BackgroundSavePath);
                _backgroundList = JsonUtility.FromJson<BackgroundProfileList>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"배경 로드 실패: {e.Message}");
            }
        }

        if (_backgroundList == null)
            _backgroundList = new BackgroundProfileList { backgrounds = new BackgroundProfile[0] };
    }

    void SaveBackgrounds()
    {
        string json = JsonUtility.ToJson(_backgroundList, true);
        File.WriteAllText(BackgroundSavePath, json);
        Debug.Log($"배경 저장 완료: {BackgroundSavePath}");
    }

    void RegisterBackground()
    {
        if (_backgroundList.backgrounds != null)
        {
            foreach (var b in _backgroundList.backgrounds)
            {
                if (b.name.Equals(_bgName, System.StringComparison.OrdinalIgnoreCase))
                {
                    EditorUtility.DisplayDialog("오류", $"'{_bgName}'은(는) 이미 등록된 이름입니다.", "확인");
                    return;
                }
            }
        }

        if (!File.Exists(_bgImagePath))
        {
            EditorUtility.DisplayDialog("오류", "이미지 파일이 존재하지 않습니다.", "확인");
            return;
        }

        // 이미지를 앱 내부 폴더로 복사
        string savedImagePath = CopyImageToStorage(_bgImagePath, BackgroundImageFolder, _bgName);
        if (string.IsNullOrEmpty(savedImagePath))
        {
            EditorUtility.DisplayDialog("오류", "이미지 복사에 실패했습니다.", "확인");
            return;
        }

        var newBg = new BackgroundProfile
        {
            id = System.Guid.NewGuid().ToString(),
            name = _bgName,
            description = _bgDescription,
            imagePath = savedImagePath  // 복사된 경로 사용
        };

        var list = new System.Collections.Generic.List<BackgroundProfile>();
        if (_backgroundList.backgrounds != null)
            list.AddRange(_backgroundList.backgrounds);
        list.Add(newBg);
        _backgroundList.backgrounds = list.ToArray();

        SaveBackgrounds();

        EditorUtility.DisplayDialog("완료", $"'{_bgName}' 배경이 등록되었습니다.\n\n사용법: @back {_bgName}\n예: @back {_bgName}에서 대화한다\n\n이미지가 앱 내부에 복사되었습니다.", "확인");

        _bgName = "";
        _bgDescription = "";
        _bgImagePath = "";
        _bgPreviewTexture = null;
    }

    void DeleteBackground(string name)
    {
        var list = new System.Collections.Generic.List<BackgroundProfile>();
        if (_backgroundList.backgrounds != null)
        {
            foreach (var b in _backgroundList.backgrounds)
            {
                if (!b.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    list.Add(b);
            }
        }
        _backgroundList.backgrounds = list.ToArray();
        SaveBackgrounds();
    }

    // ======================== 이미지 복사 유틸 ========================

    string CopyImageToStorage(string sourcePath, string targetFolder, string assetName)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return null;

        try
        {
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            string extension = Path.GetExtension(sourcePath);
            string safeAssetName = MakeSafeFileName(assetName);
            string fileName = $"{safeAssetName}_{System.Guid.NewGuid().ToString().Substring(0, 8)}{extension}";
            string targetPath = Path.Combine(targetFolder, fileName);

            File.Copy(sourcePath, targetPath, true);
            Debug.Log($"이미지 복사 완료: {sourcePath} → {targetPath}");

            return targetPath;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"이미지 복사 실패: {e.Message}");
            return null;
        }
    }

    string MakeSafeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "asset";

        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
            name = name.Replace(c, '_');

        return name;
    }
}
#endif
