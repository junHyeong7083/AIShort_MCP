using UnityEngine;
using System.Diagnostics;
using UnityEngine.Networking;
using System;
using System.Collections;
using Debug = UnityEngine.Debug;

public class PythonServerLauncher : MonoBehaviour
{
    [Header("Python 실행파일 경로")]
    [SerializeField]
    private string pythonExePath = @"C:\Users\user\miniconda3\envs\runway_upload\python.exe";

    [Header("FastAPI 서버가 있는 폴더")]
    [SerializeField]
    private string workingDirectory = @"C:\Users\user\Git\AIShort_MCP\Python";

    [Header("서버 포트")]
    [SerializeField]
    private int port = 8001;

    private Process _serverProcess;

    private void Start()
    {
        StartCoroutine(EnsureServerRunning());
    }

    private IEnumerator EnsureServerRunning()
    {
        // 1) 먼저 서버가 이미 떠 있는지 체크
        string pingUrl = $"http://127.0.0.1:{port}/docs";

        using (var req = UnityWebRequest.Get(pingUrl))
        {
            req.timeout = 1; // 1초 안에 응답 없으면 실패로 봄
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[PythonServerLauncher] 서버 이미 실행 중: {pingUrl}");
                yield break;
            }
            else
            {
                Debug.Log("[PythonServerLauncher] 서버가 안 떠있음, 새로 실행 시도");
            }
        }

        // 2) 서버 프로세스 시작 (여기서는 try/catch 안 씀)
        bool started = TryStartServerProcess();
        if (!started)
        {
            yield break;
        }

        // 3) 잠깐 기다렸다가 다시 핑 한번
        yield return new WaitForSeconds(1.5f);

        using (var req2 = UnityWebRequest.Get(pingUrl))
        {
            req2.timeout = 2;
            yield return req2.SendWebRequest();

            if (req2.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[PythonServerLauncher] 서버 실행 확인 완료");
            }
            else
            {
                Debug.LogWarning("[PythonServerLauncher] 서버 실행 확인 실패");
            }
        }
    }

    /// <summary>
    /// 실제 Python 서버 프로세스를 시작하는 함수 (try/catch는 여기서만)
    /// </summary>
    private bool TryStartServerProcess()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExePath,
                Arguments = "-m uvicorn main:app --host 127.0.0.1 --port 8001",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _serverProcess = Process.Start(psi);
            Debug.Log("[PythonServerLauncher] Python 서버 프로세스 실행 시도");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PythonServerLauncher] 서버 실행 중 예외: {e.Message}");
            return false;
        }
    }

    private void OnDestroy()
    {
        // 원하면 Unity 끌 때 서버도 같이 죽이기
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try
            {
                _serverProcess.Kill();
                _serverProcess.Dispose();
                Debug.Log("[PythonServerLauncher] 서버 프로세스 종료");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PythonServerLauncher] 서버 종료 중 예외: {e.Message}");
            }
        }
    }
}
