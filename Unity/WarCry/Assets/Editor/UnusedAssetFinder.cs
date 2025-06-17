using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 빌드 세팅의 씬들에서 사용되지 않는 에셋을 찾고 정리하는 에디터 유틸리티
/// </summary>
public class UnusedAssetFinder : EditorWindow
{
    private List<string> unusedAssets = new List<string>();
    private Vector2 scrollPosition;
    private bool includeScripts = false;
    private bool includePrefabs = false;
    private bool includeMaterials = true;
    private bool includeTextures = true;
    private bool includeAudio = true;
    private bool includeModels = true;
    private bool showConfirmation = true;
    private bool analyzeComplete = false;
    private bool isAnalyzing = false; // 분석 중 상태 플래그
    private bool deleteEmptyFolders = true; // 빈 폴더 삭제 옵션

    // 분석 결과 정보
    private int totalAssets = 0;
    private int referencedAssets = 0;
    private int emptyFoldersCount = 0; // 빈 폴더 개수
    private string[] buildScenes;
    private List<string> emptyFolders = new List<string>(); // 빈 폴더 목록

    // 캐시된 데이터
    private static string[] cachedBuildScenes = null;
    private static bool cacheValid = false;

    [MenuItem("Window/Asset Management/Unused Asset Finder")]
    public static void ShowWindow()
    {
        UnusedAssetFinder window = GetWindow<UnusedAssetFinder>("Unused Asset Finder");
        window.minSize = new Vector2(600, 400);
    }

    private void OnEnable()
    {
        // 캐시 무효화
        cacheValid = false;
    }

    private void OnGUI()
    {
        // 분석 중일 때는 간단한 UI만 표시
        if (isAnalyzing)
        {
            DrawAnalyzingUI();
            return;
        }

        EditorGUILayout.BeginVertical();

        GUILayout.Label("Unused Asset Finder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("이 도구는 빌드 설정의 씬들에서 사용되지 않는 에셋을 찾습니다.", MessageType.Info);

        GUILayout.Space(10);

        // 빌드 설정 정보 표시
        DrawBuildSceneInfo();

        GUILayout.Space(10);

        // 검색 옵션
        DrawSearchOptions();

        GUILayout.Space(10);

        // 분석 버튼
        DrawAnalyzeButton();

        // 분석 결과 표시
        if (analyzeComplete)
        {
            DrawAnalysisResults();
        }

        // 사용하지 않는 에셋 목록
        if (unusedAssets.Count > 0)
        {
            DrawUnusedAssetsList();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawAnalyzingUI()
    {
        EditorGUILayout.BeginVertical();
        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("에셋 분석 중...", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("취소", GUILayout.Width(100)))
        {
            isAnalyzing = false;
            EditorUtility.ClearProgressBar();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
    }

    private void DrawBuildSceneInfo()
    {
        string[] scenes = GetBuildScenes();

        EditorGUILayout.LabelField("빌드 설정 정보:", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField($"빌드에 포함된 씬 수: {scenes.Length}개");

        if (scenes.Length > 0 && scenes.Length <= 10) // 10개 이하일 때만 전체 표시
        {
            EditorGUILayout.LabelField("포함된 씬들:");
            EditorGUI.indentLevel++;
            foreach (string scene in scenes)
            {
                EditorGUILayout.LabelField($"• {Path.GetFileNameWithoutExtension(scene)}");
            }
            EditorGUI.indentLevel--;
        }
        else if (scenes.Length > 10)
        {
            EditorGUILayout.LabelField($"씬이 너무 많아 표시하지 않습니다. ({scenes.Length}개)");
        }
        EditorGUI.indentLevel--;
    }

    private void DrawSearchOptions()
    {
        GUILayout.Label("검색할 에셋 타입:", EditorStyles.label);

        EditorGUILayout.BeginVertical("Box");
        includeTextures = EditorGUILayout.Toggle("텍스처 (.png, .jpg, .tga 등)", includeTextures);
        includeMaterials = EditorGUILayout.Toggle("머티리얼 (.mat)", includeMaterials);
        includeAudio = EditorGUILayout.Toggle("오디오 (.wav, .mp3, .ogg 등)", includeAudio);
        includeModels = EditorGUILayout.Toggle("3D 모델 (.fbx, .obj 등)", includeModels);
        includePrefabs = EditorGUILayout.Toggle("프리팹 (.prefab)", includePrefabs);
        includeScripts = EditorGUILayout.Toggle("스크립트 (.cs) - 주의!", includeScripts);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);
        showConfirmation = EditorGUILayout.Toggle("삭제 전 확인 다이얼로그 표시", showConfirmation);
        deleteEmptyFolders = EditorGUILayout.Toggle("빈 폴더도 함께 삭제", deleteEmptyFolders);

        GUILayout.Space(5);
        EditorGUILayout.HelpBox("⚠️ 안전을 위해 분석 결과를 먼저 내보내서 검토한 후 삭제하는 것을 권장합니다.", MessageType.Warning);
    }

    private void DrawAnalyzeButton()
    {
        string[] scenes = GetBuildScenes();

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginDisabledGroup(scenes.Length == 0 || isAnalyzing);
        if (GUILayout.Button("빌드 씬에서 사용하지 않는 에셋 찾기", GUILayout.Height(35)))
        {
            StartAnalysis();
        }
        EditorGUI.EndDisabledGroup();

        // 빈 폴더만 찾기 버튼
        EditorGUI.BeginDisabledGroup(isAnalyzing);
        if (GUILayout.Button("빈 폴더만 찾기", GUILayout.Height(35), GUILayout.Width(120)))
        {
            FindEmptyFoldersOnly();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        if (scenes.Length == 0)
        {
            EditorGUILayout.HelpBox("빌드 설정에 씬이 없습니다. File → Build Settings에서 씬을 추가해주세요.", MessageType.Warning);
        }
    }

    private void DrawAnalysisResults()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("분석 결과:", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("Box");
        EditorGUILayout.LabelField($"전체 에셋 수: {totalAssets:N0}개");
        EditorGUILayout.LabelField($"사용되는 에셋: {referencedAssets:N0}개");
        EditorGUILayout.LabelField($"사용되지 않는 에셋: {unusedAssets.Count:N0}개");

        if (emptyFoldersCount > 0)
        {
            EditorGUILayout.LabelField($"빈 폴더: {emptyFoldersCount:N0}개");
        }

        if (totalAssets > 0)
        {
            float unusedPercentage = (float)unusedAssets.Count / totalAssets * 100f;
            EditorGUILayout.LabelField($"사용하지 않는 비율: {unusedPercentage:F1}%");
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawUnusedAssetsList()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField($"사용하지 않는 에셋 목록 ({unusedAssets.Count}개):", EditorStyles.boldLabel);

        // 버튼들
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("선택된 에셋 모두 삭제"))
        {
            DeleteUnusedAssets();
        }

        if (emptyFolders.Count > 0 && GUILayout.Button($"빈 폴더 삭제 ({emptyFolders.Count}개)"))
        {
            DeleteEmptyFolders();
        }

        if (GUILayout.Button("목록 지우기"))
        {
            ClearResults();
        }

        if (GUILayout.Button("분석 결과 내보내기"))
        {
            ExportResults();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // 스크롤 뷰 - 고정 높이로 설정
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

        for (int i = 0; i < unusedAssets.Count; i++)
        {
            if (DrawAssetRow(i))
            {
                // 삭제된 경우 인덱스 조정
                break; // 한 번에 하나씩만 삭제하고 다시 그리기
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private bool DrawAssetRow(int index)
    {
        if (index >= unusedAssets.Count) return false;

        string assetPath = unusedAssets[index];
        string assetName = Path.GetFileName(assetPath);

        EditorGUILayout.BeginHorizontal();

        // 아이콘과 이름
        Texture2D icon = AssetDatabase.GetCachedIcon(assetPath) as Texture2D;
        GUIContent content = new GUIContent(assetName, icon, assetPath);
        EditorGUILayout.LabelField(content, GUILayout.Width(200));

        // 경로
        EditorGUILayout.LabelField(assetPath, EditorStyles.miniLabel);

        // 파일 크기
        try
        {
            FileInfo fileInfo = new FileInfo(assetPath);
            if (fileInfo.Exists)
            {
                string sizeText = GetFileSizeString(fileInfo.Length);
                EditorGUILayout.LabelField(sizeText, EditorStyles.miniLabel, GUILayout.Width(60));
            }
        }
        catch
        {
            // 파일 정보를 가져올 수 없는 경우 무시
        }

        // 버튼들
        if (GUILayout.Button("선택", GUILayout.Width(50)))
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        bool deleted = false;
        if (GUILayout.Button("삭제", GUILayout.Width(50)))
        {
            if (DeleteSingleAsset(assetPath))
            {
                unusedAssets.RemoveAt(index);
                deleted = true;
            }
        }

        EditorGUILayout.EndHorizontal();

        return deleted;
    }

    private void StartAnalysis()
    {
        isAnalyzing = true;
        EditorApplication.delayCall += FindUnusedAssets;
    }

    private void FindUnusedAssets()
    {
        try
        {
            unusedAssets.Clear();
            analyzeComplete = false;

            string[] scenes = GetBuildScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("오류", "빌드 설정에 씬이 없습니다.", "확인");
                return;
            }

            EditorUtility.DisplayProgressBar("에셋 분석 중", "빌드 씬 분석 시작...", 0f);

            // 모든 에셋 수집
            string[] allAssetGuids = AssetDatabase.FindAssets("", new[] { "Assets" });
            totalAssets = allAssetGuids.Length;

            HashSet<string> referencedAssets = new HashSet<string>();

            // 빌드 씬들의 의존성 분석
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!isAnalyzing) break; // 취소됨

                string scenePath = scenes[i];

                EditorUtility.DisplayProgressBar("에셋 분석 중",
                    $"씬 분석 중: {Path.GetFileNameWithoutExtension(scenePath)} ({i + 1}/{scenes.Length})",
                    0.3f + (0.4f * i / scenes.Length));

                referencedAssets.Add(scenePath);
                CollectAllDependencies(scenePath, referencedAssets);
            }

            if (!isAnalyzing) return; // 취소됨

            // 중요한 폴더들의 에셋 보호
            ProtectImportantFolders(referencedAssets);

            // 사용되지 않는 에셋 찾기
            for (int i = 0; i < allAssetGuids.Length; i++)
            {
                if (!isAnalyzing) break; // 취소됨

                if (i % 100 == 0) // 100개마다 진행률 업데이트
                {
                    EditorUtility.DisplayProgressBar("에셋 분석 중",
                        $"미사용 에셋 검사 중... {i + 1}/{allAssetGuids.Length}",
                        0.7f + (0.3f * i / allAssetGuids.Length));
                }

                string assetPath = AssetDatabase.GUIDToAssetPath(allAssetGuids[i]);

                if (!referencedAssets.Contains(assetPath) && ShouldCheckAssetType(assetPath))
                {
                    unusedAssets.Add(assetPath);
                }
            }

            if (isAnalyzing) // 완료됨
            {
                this.referencedAssets = referencedAssets.Count;
                analyzeComplete = true;

                // 빈 폴더 찾기 (옵션이 활성화된 경우)
                if (deleteEmptyFolders)
                {
                    FindEmptyFolders();
                }

                // 결과 정렬 (크기 순)
                unusedAssets.Sort((a, b) => {
                    try
                    {
                        FileInfo fileA = new FileInfo(a);
                        FileInfo fileB = new FileInfo(b);
                        return fileB.Length.CompareTo(fileA.Length);
                    }
                    catch
                    {
                        return 0;
                    }
                });

                Debug.Log($"분석 완료: 전체 {totalAssets}개 에셋 중 {unusedAssets.Count}개가 빌드 씬에서 사용되지 않습니다. 빈 폴더: {emptyFoldersCount}개");
            }
        }
        finally
        {
            isAnalyzing = false;
            EditorUtility.ClearProgressBar();
            Repaint(); // UI 갱신
        }
    }

    private void CollectAllDependencies(string assetPath, HashSet<string> referencedAssets)
    {
        try
        {
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);

            foreach (string dependency in dependencies)
            {
                if (!referencedAssets.Contains(dependency))
                {
                    referencedAssets.Add(dependency);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"의존성 분석 실패 {assetPath}: {e.Message}");
        }
    }

    private void ProtectImportantFolders(HashSet<string> referencedAssets)
    {
        // 보호할 폴더들
        string[] protectedFolders = {
            "/Resources/",
            "/StreamingAssets/",
            "/Editor/",
            "/Plugins/",
            "/Gizmos/",
            "/TextMeshPro/",
            "/Settings/",
            "/Mirror/",           // Mirror 네트워킹
            "/Packages/",         // 패키지 파일들
            "/ThirdParty/",       // 서드파티 라이브러리
            "/Standard Assets/",  // Unity 표준 에셋
            "/ProBuilder/",       // ProBuilder
            "/ProGrids/",         // ProGrids
            "/XR/",              // XR 관련
            "/Analytics/",        // Unity Analytics
            "/UnityEngine/",       // Unity 엔진 관련
            "/MapMagic/"
        };

        // 보호할 시스템 파일들 (파일명 기준)
        string[] protectedSystemFiles = {
            "RenderPipelineGlobalSettings",
            "UniversalRenderPipelineAsset",
            "UniversalRenderPipelineGlobalSettings",
            "HDRenderPipelineGlobalSettings",
            "HDRenderPipelineAsset",
            "GraphicsSettings",
            "ProjectSettings",
            "TMP Settings",
            "TMP_DefaultShader",
            "LiberationSans",
            "Unity Font Asset",
            "Default UI Material",
            "NetworkManager",     // Mirror 관련
            "NetworkBehaviour",   // Mirror 관련
            "SyncVar",           // Mirror 관련
            "Mirror"             // Mirror 관련 모든 파일
        };

        // 보호할 확장자들
        string[] protectedExtensions = {
            ".inputactions", // Input System
            ".preset",       // Presets
            ".lighting",     // Lighting Settings
            ".shadergraph",  // Shader Graph
            ".shadersubgraph", // Shader Sub Graph
            ".asmdef",       // Assembly Definition
            ".asmref",       // Assembly Reference
        };

        // 보호할 셰이더 키워드들
        string[] protectedShaderKeywords = {
            "TextMeshPro",
            "TMP",
            "UI",
            "Sprites",
            "Default",
            "Universal Render Pipeline",
            "URP",
            "Lit",
            "Unlit",
            "Legacy Shaders",
            "Mirror",
            "Network"
        };

        // 보호할 네임스페이스/패키지 키워드들
        string[] protectedPackageKeywords = {
            "com.unity.",        // Unity 공식 패키지
            "com.vis2k.mirror",  // Mirror 패키지
            "Unity.Mirror",      // Mirror 네임스페이스
            "Mirror.Editor",     // Mirror 에디터
            "NetworkManager",    // 네트워크 관련
            "NetworkBehaviour",  // 네트워크 관련
            "System.",          // 시스템 네임스페이스
            "UnityEngine.",     // Unity 엔진
            "UnityEditor."      // Unity 에디터
        };

        try
        {
            string[] allAssets = AssetDatabase.GetAllAssetPaths();

            foreach (string assetPath in allAssets)
            {
                bool shouldProtect = false;
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                string extension = Path.GetExtension(assetPath).ToLower();

                // 1. 폴더별 보호
                foreach (string folder in protectedFolders)
                {
                    if (assetPath.Contains(folder))
                    {
                        shouldProtect = true;
                        break;
                    }
                }

                // 2. 패키지 관련 파일 보호
                if (!shouldProtect)
                {
                    foreach (string packageKeyword in protectedPackageKeywords)
                    {
                        if (assetPath.Contains(packageKeyword) || fileName.Contains(packageKeyword))
                        {
                            shouldProtect = true;
                            break;
                        }
                    }
                }

                // 3. 시스템 파일 보호
                if (!shouldProtect)
                {
                    foreach (string systemFile in protectedSystemFiles)
                    {
                        if (fileName.Contains(systemFile))
                        {
                            shouldProtect = true;
                            break;
                        }
                    }
                }

                // 4. 확장자별 보호
                if (!shouldProtect)
                {
                    if (protectedExtensions.Contains(extension))
                    {
                        shouldProtect = true;
                    }
                }

                // 5. 셰이더 파일 특별 보호
                if (!shouldProtect && extension == ".shader")
                {
                    foreach (string keyword in protectedShaderKeywords)
                    {
                        if (assetPath.Contains(keyword) || fileName.Contains(keyword))
                        {
                            shouldProtect = true;
                            break;
                        }
                    }
                }

                // 6. 스크립트 파일 특별 보호 (.cs 파일 중 중요한 것들)
                if (!shouldProtect && extension == ".cs")
                {
                    foreach (string packageKeyword in protectedPackageKeywords)
                    {
                        if (assetPath.Contains(packageKeyword) || fileName.Contains(packageKeyword))
                        {
                            shouldProtect = true;
                            break;
                        }
                    }
                }

                if (shouldProtect)
                {
                    referencedAssets.Add(assetPath);
                    if (assetPath.Contains("Mirror") || assetPath.Contains("Network"))
                    {
                        Debug.Log($"네트워크 관련 파일 보호: {assetPath}");
                    }
                    else if (assetPath.Contains("Shader") || assetPath.Contains("TMP") || assetPath.Contains("TextMeshPro"))
                    {
                        Debug.Log($"중요 시스템 파일 보호: {assetPath}");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"중요 폴더 보호 처리 실패: {e.Message}");
        }
    }

    private string[] GetBuildScenes()
    {
        if (!cacheValid || cachedBuildScenes == null)
        {
            cachedBuildScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            cacheValid = true;
        }

        return cachedBuildScenes;
    }

    private bool ShouldCheckAssetType(string assetPath)
    {
        if (Directory.Exists(assetPath) || assetPath.EndsWith(".meta"))
            return false;

        if (assetPath.StartsWith("ProjectSettings/"))
            return false;

        string extension = Path.GetExtension(assetPath).ToLower();

        if (extension == ".cs") return includeScripts;
        if (IsTextureFile(extension)) return includeTextures;
        if (extension == ".mat") return includeMaterials;
        if (IsAudioFile(extension)) return includeAudio;
        if (IsModelFile(extension)) return includeModels;
        if (extension == ".prefab") return includePrefabs;

        return true;
    }

    private bool IsTextureFile(string extension)
    {
        string[] textureExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tiff", ".bmp", ".gif", ".exr", ".hdr" };
        return textureExtensions.Contains(extension);
    }

    private bool IsAudioFile(string extension)
    {
        string[] audioExtensions = { ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".flac", ".m4a" };
        return audioExtensions.Contains(extension);
    }

    private bool IsModelFile(string extension)
    {
        string[] modelExtensions = { ".fbx", ".obj", ".dae", ".blend", ".3ds", ".max", ".ma", ".mb" };
        return modelExtensions.Contains(extension);
    }

    private void DeleteUnusedAssets()
    {
        if (unusedAssets.Count == 0) return;

        bool shouldDelete = true;

        if (showConfirmation)
        {
            long totalSize = 0;
            try
            {
                totalSize = unusedAssets.Sum(path => {
                    FileInfo file = new FileInfo(path);
                    return file.Exists ? file.Length : 0;
                });
            }
            catch
            {
                // 크기 계산 실패 시 무시
            }

            string sizeText = GetFileSizeString(totalSize);

            shouldDelete = EditorUtility.DisplayDialog(
                "에셋 삭제 확인",
                $"{unusedAssets.Count}개의 미사용 에셋을 삭제하시겠습니까?\n\n" +
                $"총 용량: {sizeText}\n\n" +
                "이 작업은 되돌릴 수 없습니다!",
                "삭제",
                "취소");
        }

        if (!shouldDelete) return;

        int deletedCount = 0;
        List<string> failedDeletions = new List<string>();

        try
        {
            for (int i = 0; i < unusedAssets.Count; i++)
            {
                EditorUtility.DisplayProgressBar("에셋 삭제 중",
                    $"삭제 중: {i + 1}/{unusedAssets.Count}",
                    (float)i / unusedAssets.Count);

                try
                {
                    if (AssetDatabase.DeleteAsset(unusedAssets[i]))
                    {
                        deletedCount++;
                    }
                    else
                    {
                        failedDeletions.Add(unusedAssets[i]);
                    }
                }
                catch (System.Exception e)
                {
                    failedDeletions.Add(unusedAssets[i]);
                    Debug.LogError($"삭제 실패 {unusedAssets[i]}: {e.Message}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        string message = $"{deletedCount}개 에셋이 성공적으로 삭제되었습니다.";
        if (failedDeletions.Count > 0)
        {
            message += $"\n{failedDeletions.Count}개 에셋을 삭제할 수 없었습니다.";
        }

        EditorUtility.DisplayDialog("삭제 완료", message, "확인");
        ClearResults();
    }

    private bool DeleteSingleAsset(string assetPath)
    {
        bool shouldDelete = true;

        if (showConfirmation)
        {
            string sizeText = "";
            try
            {
                FileInfo fileInfo = new FileInfo(assetPath);
                sizeText = fileInfo.Exists ? GetFileSizeString(fileInfo.Length) : "";
            }
            catch
            {
                // 크기 정보 가져오기 실패 시 무시
            }

            shouldDelete = EditorUtility.DisplayDialog(
                "에셋 삭제",
                $"{Path.GetFileName(assetPath)} 파일을 삭제하시겠습니까?\n\n크기: {sizeText}",
                "삭제",
                "취소");
        }

        if (shouldDelete)
        {
            try
            {
                if (AssetDatabase.DeleteAsset(assetPath))
                {
                    AssetDatabase.Refresh();
                    return true;
                }
                else
                {
                    EditorUtility.DisplayDialog("오류", $"{assetPath} 삭제에 실패했습니다.", "확인");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("오류", $"삭제 중 오류 발생:\n{e.Message}", "확인");
            }
        }

        return false;
    }

    private void ClearResults()
    {
        unusedAssets.Clear();
        emptyFolders.Clear();
        emptyFoldersCount = 0;
        analyzeComplete = false;
        cacheValid = false; // 캐시 무효화
        Repaint();
    }

    private void ExportResults()
    {
        if (unusedAssets.Count == 0) return;

        string path = EditorUtility.SaveFilePanel("분석 결과 저장", "", "UnusedAssets", "txt");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.WriteLine($"빌드 씬에서 사용하지 않는 에셋 분석 결과");
                writer.WriteLine($"분석 일시: {System.DateTime.Now}");
                writer.WriteLine($"전체 에셋: {totalAssets}개");
                writer.WriteLine($"사용되는 에셋: {referencedAssets}개");
                writer.WriteLine($"사용되지 않는 에셋: {unusedAssets.Count}개");
                writer.WriteLine($"빈 폴더: {emptyFoldersCount}개");
                writer.WriteLine();

                writer.WriteLine("빌드에 포함된 씬:");
                foreach (string scene in GetBuildScenes())
                {
                    writer.WriteLine($"  - {scene}");
                }
                writer.WriteLine();

                if (emptyFolders.Count > 0)
                {
                    writer.WriteLine("빈 폴더 목록:");
                    foreach (string folder in emptyFolders)
                    {
                        writer.WriteLine($"{folder}");
                    }
                    writer.WriteLine();
                }

                writer.WriteLine("사용되지 않는 에셋 목록:");
                foreach (string asset in unusedAssets)
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(asset);
                        string size = fileInfo.Exists ? GetFileSizeString(fileInfo.Length) : "N/A";
                        writer.WriteLine($"{asset} ({size})");
                    }
                    catch
                    {
                        writer.WriteLine($"{asset} (크기 정보 없음)");
                    }
                }
            }

            EditorUtility.DisplayDialog("내보내기 완료", $"결과가 저장되었습니다:\n{path}", "확인");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("오류", $"파일 저장 실패:\n{e.Message}", "확인");
        }
    }

    private string GetFileSizeString(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    #region Empty Folders Management

    /// <summary>
    /// 빈 폴더만 찾기 (에셋 분석 없이)
    /// </summary>
    private void FindEmptyFoldersOnly()
    {
        isAnalyzing = true;
        EditorApplication.delayCall += () => {
            try
            {
                emptyFolders.Clear();
                analyzeComplete = false;

                EditorUtility.DisplayProgressBar("빈 폴더 검색 중", "폴더 검사 중...", 0f);

                FindEmptyFolders();
                analyzeComplete = true;

                Debug.Log($"빈 폴더 검색 완료: {emptyFoldersCount}개 발견");
            }
            finally
            {
                isAnalyzing = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        };
    }

    /// <summary>
    /// 빈 폴더 찾기
    /// </summary>
    private void FindEmptyFolders()
    {
        emptyFolders.Clear();

        try
        {
            string[] allFolders = AssetDatabase.GetSubFolders("Assets");
            List<string> foldersToCheck = new List<string>(allFolders);

            // 재귀적으로 모든 하위 폴더 수집
            for (int i = 0; i < foldersToCheck.Count; i++)
            {
                string[] subFolders = AssetDatabase.GetSubFolders(foldersToCheck[i]);
                foldersToCheck.AddRange(subFolders);
            }

            // 각 폴더가 비어있는지 확인
            foreach (string folder in foldersToCheck)
            {
                if (IsFolderEmpty(folder) && !IsProtectedFolder(folder))
                {
                    emptyFolders.Add(folder);
                }
            }

            emptyFoldersCount = emptyFolders.Count;

            // 깊은 폴더부터 삭제하도록 정렬 (하위 폴더 먼저)
            emptyFolders.Sort((a, b) => b.Length.CompareTo(a.Length));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"빈 폴더 검색 중 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 폴더가 비어있는지 확인
    /// </summary>
    private bool IsFolderEmpty(string folderPath)
    {
        try
        {
            // 에셋 파일 확인
            string[] assets = AssetDatabase.FindAssets("", new[] { folderPath });
            if (assets.Length > 0)
            {
                // .meta 파일이 아닌 실제 에셋이 있는지 확인
                foreach (string guid in assets)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!assetPath.EndsWith(".meta") && assetPath.StartsWith(folderPath))
                    {
                        return false;
                    }
                }
            }

            // 하위 폴더 확인
            string[] subFolders = AssetDatabase.GetSubFolders(folderPath);
            return subFolders.Length == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 보호되어야 하는 폴더인지 확인
    /// </summary>
    private bool IsProtectedFolder(string folderPath)
    {
        string[] protectedFolders = {
            "Assets/Resources",
            "Assets/StreamingAssets",
            "Assets/Editor",
            "Assets/Plugins",
            "Assets/Gizmos",
            "Assets/TextMeshPro",
            "Assets/Settings"
        };

        foreach (string protectedFolder in protectedFolders)
        {
            if (folderPath.StartsWith(protectedFolder))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 빈 폴더들 삭제
    /// </summary>
    private void DeleteEmptyFolders()
    {
        if (emptyFolders.Count == 0) return;

        bool shouldDelete = true;

        if (showConfirmation)
        {
            shouldDelete = EditorUtility.DisplayDialog(
                "빈 폴더 삭제 확인",
                $"{emptyFolders.Count}개의 빈 폴더를 삭제하시겠습니까?\n\n" +
                "이 작업은 되돌릴 수 없습니다!",
                "삭제",
                "취소");
        }

        if (!shouldDelete) return;

        int deletedCount = 0;
        List<string> failedDeletions = new List<string>();

        try
        {
            for (int i = 0; i < emptyFolders.Count; i++)
            {
                EditorUtility.DisplayProgressBar("빈 폴더 삭제 중",
                    $"삭제 중: {i + 1}/{emptyFolders.Count}",
                    (float)i / emptyFolders.Count);

                try
                {
                    // 삭제 전 다시 한 번 비어있는지 확인
                    if (IsFolderEmpty(emptyFolders[i]))
                    {
                        if (AssetDatabase.DeleteAsset(emptyFolders[i]))
                        {
                            deletedCount++;
                        }
                        else
                        {
                            failedDeletions.Add(emptyFolders[i]);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    failedDeletions.Add(emptyFolders[i]);
                    Debug.LogError($"폴더 삭제 실패 {emptyFolders[i]}: {e.Message}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        string message = $"{deletedCount}개의 빈 폴더가 성공적으로 삭제되었습니다.";
        if (failedDeletions.Count > 0)
        {
            message += $"\n{failedDeletions.Count}개 폴더를 삭제할 수 없었습니다.";
        }

        EditorUtility.DisplayDialog("삭제 완료", message, "확인");

        // 빈 폴더 목록 갱신
        emptyFolders.Clear();
        emptyFoldersCount = 0;
        Repaint();
    }

    #endregion
}