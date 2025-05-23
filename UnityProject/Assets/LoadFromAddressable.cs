using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Unity.Networking;
using Unity.VisualScripting;
using UnityEngine.Networking;
using Cache = UnityEngine.Cache;

public class LoadFromAddressable : MonoBehaviour
{
    public TextMeshProUGUI progressText;

    private bool shouldInstantiatePrefabs;
    private bool isDownloading;
    List<string> fileNames = new();
    
    public async void Download()
    {
        Caching.ClearCache();
        UnityWebRequest.ClearCookieCache();
        await Addressables.InitializeAsync();
        var sizeHandle = await Addressables.GetDownloadSizeAsync("Assets/Prefabs/Capsule.prefab");
        Debug.Log(sizeHandle);
        var allKeys = Addressables.ResourceLocators.SelectMany(x => x.Keys).Distinct().ToList();
        var locationsHandle = await Addressables.LoadResourceLocationsAsync(allKeys, Addressables.MergeMode.Union);
        foreach (var location in locationsHandle)
        {
            string normalizedInternalId = location.InternalId.Replace("\\", "/");

            if (normalizedInternalId.Contains(Application.persistentDataPath))
            {
                var fileName = Path.GetFileName(location.InternalId);
                fileNames.Add(fileName);
            }
        }
        
        StartCoroutine(DownloadBundle());
    }

    IEnumerator DownloadBundle()
    {
        string destinationDirectory = Path.Combine(Application.persistentDataPath, "com.unity.addressable");
        if (!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        int totalFiles = fileNames.Count;
        int completedFiles = 0;
        float totalProgress = 0f;

        List<BackgroundDownload> downloads = new List<BackgroundDownload>();

        foreach (var name in fileNames)
        {
            Uri bundleUri = new Uri($"https://github.com/hmthangGitHub/BackgroundDownloadAddressableTest/raw/refs/heads/master/docs/{name}");
            var download = BackgroundDownload.Start(bundleUri, name);
            downloads.Add(download);
        }
        
        while (downloads.Any(d => d.status == BackgroundDownloadStatus.Downloading))
        {
            totalProgress = 0f;
            completedFiles = downloads.Count(d => d.status == BackgroundDownloadStatus.Done);
            foreach (var download in downloads)
            {
                if (download.status == BackgroundDownloadStatus.Downloading)
                {
                    totalProgress += download.progress / 1000000.0f;
                }
                else if (download.status == BackgroundDownloadStatus.Done)
                {
                    totalProgress += 100.0f;
                }
            }

            float averageProgress = totalProgress / totalFiles;
            progressText.text = $"Downloading {completedFiles}/{totalFiles} files: {averageProgress:0.0}%";
            yield return null;
        }
        
        for (int i = 0; i < downloads.Count; i++)
        {
            var download = downloads[i];
            var name = fileNames[i];
            string filePath = Path.Combine(Application.persistentDataPath, name);
            string destinationPath = Path.Combine(destinationDirectory, name);
        
            if (download.status == BackgroundDownloadStatus.Done)
            {
                if (File.Exists(filePath))
                {
                    File.Move(filePath, destinationPath);
                }
            }
            else
            {
                Debug.LogError($"Failed to download {name}: {download.error}");
                progressText.text = $"Failed to download {name}!";
                yield break;
            }
        }

        progressText.text = "All files downloaded!";
        Addressables.InstantiateAsync("Assets/Prefabs/character.fbx");
    }

    public void DeleteFile()
    {
        foreach (var directory in Directory.GetDirectories(Application.persistentDataPath))
        {
            Directory.Delete(directory, true);
        }

        foreach (var file in Directory.GetFiles(Application.persistentDataPath))
        {
            File.Delete(file);
        }

        Debug.Log("All files deleted.");
    }
    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Open Persistent Data Path")]
    private static void OpenPersistentDataPath()
    {
        string path = Application.persistentDataPath;
        if (Directory.Exists(path))
        {
            System.Diagnostics.Process.Start(path);
        }
        else
        {
            Debug.LogError($"Directory not found: {path}");
        }
    }

    [UnityEditor.MenuItem("Tools/Open StreamingAssets Path")]
    private static void OpenStreamingAssetsPath()
    {
        string path = Application.streamingAssetsPath;
        if (Directory.Exists(path))
        {
            System.Diagnostics.Process.Start(path);
        }
        else
        {
            Debug.LogError($"Directory not found: {path}");
        }
    }
    #endif
}