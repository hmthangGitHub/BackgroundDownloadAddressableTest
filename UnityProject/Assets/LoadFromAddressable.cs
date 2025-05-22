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

    private const string ZipFileName = "a.zip";
    private const string DummyFileName = "test.mp4";
    private const string CatalogFileName = "catalog_0.1.json";

    private string ZipPath => Path.Combine(Application.persistentDataPath, ZipFileName);
    private string DummyPath => Path.Combine(Application.persistentDataPath, DummyFileName);
    private string ExtractedPath => Path.Combine(Application.persistentDataPath, "Extracted");
    private string CatalogPath => Path.Combine(ExtractedPath, CatalogFileName);

    private bool shouldInstantiatePrefabs;
    private List<GameObject> loadedPrefabs = new();
    private bool isDownloading;

    void Update()
    {
        var downloads = BackgroundDownload.backgroundDownloads;
         if (downloads.Length > 0 && downloads[0].status == BackgroundDownloadStatus.Downloading)
         {
             var download = downloads[0];
             long progress = download.progress;
             progressText.text = progress >= 0
                 ? $"Downloading: {progress / 1000000.0f:0.0}%"
                 : "Downloading...";
         }

        if (shouldInstantiatePrefabs && loadedPrefabs.Count > 0)
        {
            foreach (var prefab in loadedPrefabs)
            {
                Instantiate(prefab);
            }
            progressText.text = $"Instantiated {loadedPrefabs.Count} prefabs!";
            shouldInstantiatePrefabs = false;
            isDownloading = false;
        }
    }

    public async void Download()
    {
        Caching.ClearCache();
        await Addressables.InitializeAsync();
        var sizeHandle = await Addressables.GetDownloadSizeAsync("Assets/Prefabs/Capsule.prefab");
        Debug.Log(sizeHandle);
        var allKeys = Addressables.ResourceLocators.SelectMany(x => x.Keys).Distinct().ToList();
        var locationsHandle = await Addressables.LoadResourceLocationsAsync(allKeys, Addressables.MergeMode.Union);
        string path = Path.Combine(Application.persistentDataPath, "com.unity.addressables");

        await Addressables.DownloadDependenciesAsync("Assets/Prefabs/Capsule.prefab");
        await Addressables.InstantiateAsync("Assets/Prefabs/Capsule.prefab");
        Debug.Log(Application.persistentDataPath);
        

        if (Directory.Exists(path))
        {
            string[] files = Directory.GetFiles(path);

            foreach (string file in files)
            {
                Debug.Log("File: " + Path.GetFileName(file));
            }
        }
    }

    IEnumerator DownloadMultipleFiles()
    {
        Uri zipUrl = new Uri("https://drive.google.com/uc?export=download&id=1mV8DPjQmjHGMEAhLLtgF2830fP-p7RqB");
        var zipDownload = BackgroundDownload.Start(zipUrl, ZipFileName);
        
        // Uri dummyUrl = new Uri("https://drive.google.com/uc?export=download&id=1DHneI6DrS6CBxehr5C4x0J0X9BiUGVzo");
        // var dummyDownload = BackgroundDownload.Start(dummyUrl, DummyFileName);

        // yield return dummyDownload;
        yield return zipDownload;

        // if (dummyDownload.status != BackgroundDownloadStatus.Done)
        //     Debug.LogError($"Dummy file download failed: {dummyDownload.error}");

        if (zipDownload.status == BackgroundDownloadStatus.Done)
        {
            Debug.Log("ZIP file downloaded. Extracting...");
            StartCoroutine(ExtractAndLoadCatalog());
        }
        else
        {
            Debug.LogError("ZIP file download failed: " + zipDownload.error);
            progressText.text = "Download failed!";
        }
    }

    IEnumerator ExtractAndLoadCatalog()
    {
        //Extract file Zip
        if (Directory.Exists(ExtractedPath))
            Directory.Delete(ExtractedPath, true);

        try
        {
            ZipFile.ExtractToDirectory(ZipPath, ExtractedPath);
            Debug.Log("Extracted to: " + ExtractedPath);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to extract zip: " + ex.Message);
            progressText.text = "Extract failed!";
            yield break;
        }

        //Load catalog
        string catalogFullPath = "file://" + CatalogPath;

        var handle = Addressables.LoadContentCatalogAsync(catalogFullPath);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log("Catalog loaded.");

            List<object> prefabKeys = new();
            foreach (var key in handle.Result.Keys)
            {
                if (handle.Result.Locate(key, typeof(GameObject), out _))
                {
                    prefabKeys.Add(key);
                }
            }

            if (prefabKeys.Count == 0)
            {
                progressText.text = "No prefabs found!";
                yield break;
            }
            
            foreach (var locator in Addressables.ResourceLocators) 
            { 
                foreach (var key in locator.Keys) 
                { 
                    if (locator.Locate(key, typeof(GameObject), out var locations)) 
                    { 
                        foreach (var location in locations) 
                        { 
                            string url = location.InternalId; 
                            Debug.Log($"Key: {key} → URL: {url}"); 
                        } 
                    } 
                } 
            }

            loadedPrefabs.Clear();
            foreach (var key in prefabKeys)
            {
                var prefabHandle = Addressables.LoadAssetAsync<GameObject>(key);
                yield return prefabHandle;

                if (prefabHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    loadedPrefabs.Add(prefabHandle.Result);
                }
            }

            if (loadedPrefabs.Count > 0)
            {
                shouldInstantiatePrefabs = true;
                Debug.Log($"Loaded {loadedPrefabs.Count} prefabs, ready to instantiate.");
            }
            else
            {
                Debug.LogError("Failed to load any prefabs.");
                progressText.text = "Prefab load failed!";
            }
        }
        else
        {
            Debug.LogError("Failed to load catalog.");
            progressText.text = "Catalog load failed!";
        }
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
}