using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Unity.Networking; // From Unity-Technologies/BackgroundDownload package

/// <summary>
/// Custom asynchronous operation for handling background downloads via the BackgroundDownload package.
/// </summary>
/// <typeparam name="TObject">The type of asset to be loaded (e.g., byte, AssetBundle).</typeparam>
public class BackgroundDownloadOperation<TObject> : AsyncOperationBase<TObject> where TObject : class
{
    private IResourceLocation m_Location;
    private BackgroundDownload m_BackgroundDownload;
    private string m_DownloadFilePath;
    private Coroutine m_MonitorCoroutine;

    // A flag to indicate if the operation is currently running or has completed.
    private bool m_IsDone = false;

    /// <summary>
    /// Constructor for a new download operation.
    /// </summary>
    /// <param name="location">The IResourceLocation for the asset.</param>
    public BackgroundDownloadOperation(IResourceLocation location)
    {
        m_Location = location;
        // Derive a unique and persistent file path for the download.
        // Using a hash of the URL helps prevent conflicts and ensures uniqueness.
        string fileName = Path.GetFileName(location.InternalId);
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = Guid.NewGuid().ToString(); // Fallback to GUID if URL doesn't have a clear filename
        }
        m_DownloadFilePath = Path.Combine(Application.persistentDataPath, "AddressableDownloads", fileName);

        // Ensure the directory exists
        string directory = Path.GetDirectoryName(m_DownloadFilePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Constructor for resuming an existing background download.
    /// </summary>
    /// <param name="location">The IResourceLocation for the asset.</param>
    /// <param name="existingDownload">An existing BackgroundDownload instance to resume.</param>
    public BackgroundDownloadOperation(IResourceLocation location, BackgroundDownload existingDownload) : this(location)
    {
        m_BackgroundDownload = existingDownload;
    }

    protected override string DebugName => $"BackgroundDownloadOperation: {m_Location.InternalId}";

    protected override float Progress
    {
        get
        {
            // BackgroundDownload.progress can be expensive on Android,
            // so we only query it when the operation is active.
            if (m_BackgroundDownload!= null &&!m_IsDone)
            {
                return m_BackgroundDownload.progress;
            }
            return 0f; // Default progress if not started or done
        }
    }

    /// <summary>
    /// Called by ResourceManager to start the operation's work.
    /// </summary>
    protected override void Execute()
    {
        if (m_BackgroundDownload == null)
        {
            // Attempt to find an existing download for this URL.
            // This is crucial for resuming downloads after app restarts.
            foreach (var bd in BackgroundDownload.backgroundDownloads)
            {
                if (bd.config.url.ToString() == m_Location.InternalId)
                {
                    m_BackgroundDownload = bd;
                    Debug.Log($"Resuming existing background download for: {m_Location.InternalId}");
                    break;
                }
            }
        }

        if (m_BackgroundDownload == null)
        {
            // No existing download found, start a new one.
            Uri downloadUri;
            try
            {
                downloadUri = new Uri(m_Location.InternalId);
            }
            catch (UriFormatException e)
            {
                Complete(default(TObject), false, $"Invalid URL format for download: {m_Location.InternalId}. Error: {e.Message}");
                m_IsDone = true;
                return;
            }

            BackgroundDownloadConfig config = new BackgroundDownloadConfig
            {
                url = downloadUri,
                filePath = Path.GetFileName(m_DownloadFilePath), // BackgroundDownload expects relative path
                // policy = BackgroundDownloadPolicy.AllowMetered, // Policy does not persist and not supported on iOS
                // requestHeaders = new Dictionary<string, List<string>>() // Headers do not persist
            };

            try
            {
                m_BackgroundDownload = BackgroundDownload.Start(config);
                Debug.Log($"Started new background download for: {m_Location.InternalId} to {m_DownloadFilePath}");
            }
            catch (Exception e)
            {
                Complete(default(TObject), false, $"Failed to start background download for {m_Location.InternalId}. Error: {e.Message}");
                m_IsDone = true;
                return;
            }
        }

        // Start a coroutine to monitor the background download's status.
        // This coroutine will yield until the download completes or fails.
        m_MonitorCoroutine = CoroutineStarter.StartCoroutineStatic(MonitorDownload());
    }

    /// <summary>
    /// Coroutine to monitor the BackgroundDownload status and complete the AsyncOperationBase.
    /// </summary>
    private IEnumerator MonitorDownload()
    {
        // Yield control to the BackgroundDownload object until it completes.
        // This allows the native download process to run in the background.
        yield return m_BackgroundDownload;

        m_IsDone = true; // Mark as done for progress reporting

        if (m_BackgroundDownload.status == BackgroundDownloadStatus.Done)
        {
            // try
            {
                // The BackgroundDownload package only downloads the file to disk.
                // We are responsible for loading that file into the desired Unity asset type (TObject).
                TObject loadedAsset = null;

                if (typeof(TObject) == typeof(byte))
                {
                    // If the requested type is byte, simply read the file.
                    loadedAsset = File.ReadAllBytes(m_DownloadFilePath) as TObject;
                }
                else if (typeof(TObject) == typeof(AssetBundle))
                {
                    // If the requested type is AssetBundle, load it from the downloaded file.
                    // This is an asynchronous operation itself.
                    var assetBundleCreateRequest = AssetBundle.LoadFromFileAsync(m_DownloadFilePath);
                    yield return assetBundleCreateRequest; // Wait for AssetBundle to load
                    loadedAsset = assetBundleCreateRequest.assetBundle as TObject;
                }
                // Add more type handling here (e.g., Texture2D, AudioClip) if needed.
                // For other types, you might need to load raw bytes and then convert.
                else
                {
                    // Fallback for unsupported types, or if TObject is not AssetBundle/byte
                    Complete(null, false, $"Unsupported asset type for BackgroundDownloadProvider: {typeof(TObject).Name}");
                    yield break;
                }

                if (loadedAsset!= null)
                {
                    Complete(loadedAsset, true, null);
                }
                else
                {
                    Complete(null, false, $"Failed to load asset from downloaded file: {m_DownloadFilePath}");
                }
            }
            // catch (Exception e)
            // {
            //     Complete(null, false, $"Error processing downloaded file {m_DownloadFilePath}. Error: {e.Message}");
            // }
        }
        else if (m_BackgroundDownload.status == BackgroundDownloadStatus.Failed)
        {
            Complete(null, false, m_BackgroundDownload.error);
        }

        // The BackgroundDownload object must be disposed after completion to free system resources.
        // This is handled in the Destroy() method, which is called when the Addressables handle is released.
    }

    /// <summary>
    /// Called by ResourceManager when the operation's reference count reaches zero.
    /// This is where resources should be released.
    /// </summary>
    protected override void Destroy()
    {
        if (m_MonitorCoroutine!= null)
        {
            CoroutineStarter.StopCoroutineStatic(m_MonitorCoroutine);
            m_MonitorCoroutine = null;
        }

        if (m_BackgroundDownload!= null)
        {
            // Dispose the native background download object to free system resources.
            // If the download is still in progress, this will cancel it.
            m_BackgroundDownload.Dispose();
            m_BackgroundDownload = null;
            Debug.Log($"Disposed BackgroundDownload for {m_Location.InternalId}");
        }

        // Clean up internal references
        m_Location = null;
        m_DownloadFilePath = null;
        Result = null; // Clear the result
    }
}