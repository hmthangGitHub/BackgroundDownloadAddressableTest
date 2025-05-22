using System;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Networking;
using UnityEngine.ResourceManagement.ResourceLocations;

[DisplayName(nameof(BackgroundDownloadAssetBundleProvider))]
public class BackgroundDownloadAssetBundleProvider : AssetBundleProvider
{
    private Dictionary<string, ProvideHandle> _activeDownloads = new Dictionary<string, ProvideHandle>();

    public override void Provide(ProvideHandle provideHandle)
    {
        if (IsRemoteLocation(provideHandle.Location))
        {
            StartBackgroundDownload(provideHandle);
        }
        else
        {
            base.Provide(provideHandle);
        }
    }

    private bool IsRemoteLocation(IResourceLocation location)
    {
        return location.InternalId.StartsWith("http");
    }

    private void StartBackgroundDownload(ProvideHandle provideHandle)
    {
        string url = provideHandle.Location.InternalId;
        string filePath = GetCachedFilePath(url); // Implement caching logic

        // Start background download
        var download = BackgroundDownload.Start(
            new Uri(url),
            filePath
        );

        // download.ProgressUpdated += (progress) => 
        // {
        //     provideHandle.ReportProgress(progress);
        // };

        // download. += (status) => 
        // {
        //     if (status == BackgroundDownloadStatus.Succeeded)
        //     {
        //         LoadAssetBundleFromFile(filePath, provideHandle);
        //     }
        //     else
        //     {
        //         provideHandle.Complete<AssetBundle>(null, false, new System.Exception("Download failed"));
        //     }
        // };

        _activeDownloads[url] = provideHandle;
    }

    private void LoadAssetBundleFromFile(string filePath, ProvideHandle provideHandle)
    {
        // // Must load AssetBundle on the main thread
        // UnityMainThreadDispatcher.Instance.Enqueue(() =>
        // {
        //     var loadOperation = AssetBundle.LoadFromFileAsync(filePath);
        //     loadOperation.completed += (op) =>
        //     {
        //         provideHandle.Complete(loadOperation.assetBundle, true, null);
        //     };
        // });
    }

    private string GetCachedFilePath(string url)
    {
        // Use Addressables' cache path or a custom directory
        return UnityEngine.Caching.currentCacheForWriting.path + "/" + Hash128.Compute(url);
    }
}