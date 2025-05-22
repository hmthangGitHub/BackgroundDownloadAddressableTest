// #if UNITY_2022_1_OR_NEWER
// #define UNLOAD_BUNDLE_ASYNC
// #endif
//
// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.ComponentModel;
// using System.IO;
// using System.Linq;
// using UnityEngine;
// using UnityEngine.Networking;
// using UnityEngine.Profiling;
// using UnityEngine.ResourceManagement;
// using UnityEngine.ResourceManagement.AsyncOperations;
// using UnityEngine.ResourceManagement.Exceptions;
// using UnityEngine.ResourceManagement.ResourceLocations;
// using UnityEngine.ResourceManagement.ResourceProviders;
// using UnityEngine.ResourceManagement.Util;
// using UnityEngine.Serialization;
// using AsyncOperation = UnityEngine.AsyncOperation;
//
//
// namespace AddressableBackgroundDownload
// {
//      internal interface IOperationCacheKey : IEquatable<IOperationCacheKey>
//     {
//     }
//
//     /// <summary>
//     /// Used to compare cachable operation based solely on a single string id
//     /// </summary>
//     internal sealed class IdCacheKey : IOperationCacheKey
//     {
//         public string ID;
//         public Type locationType;
//
//         public IdCacheKey(Type locType, string id)
//         {
//             ID = id;
//             locationType = locType;
//         }
//
//         bool Equals(IdCacheKey other)
//         {
//             if (ReferenceEquals(this, other)) return true;
//             if (ReferenceEquals(other, null)) return false;
//
//             return other.ID == ID && locationType == other.locationType;
//         }
//
//         public override int GetHashCode()
//         {
//             return (17 * 31 + ID.GetHashCode()) * 31 + locationType.GetHashCode();
//         }
//
//         public override bool Equals(object obj) => Equals(obj as IdCacheKey);
//         public bool Equals(IOperationCacheKey other) => Equals(other as IdCacheKey);
//     }
//
//     internal sealed class LocationCacheKey : IOperationCacheKey
//     {
//         readonly IResourceLocation m_Location;
//         readonly Type m_DesiredType;
//
//         public LocationCacheKey(IResourceLocation location, Type desiredType)
//         {
//             if (location == null)
//                 throw new NullReferenceException($"Resource location cannot be null.");
//             if (desiredType == null)
//                 throw new NullReferenceException($"Desired type cannot be null.");
//
//             m_Location = location;
//             m_DesiredType = desiredType;
//         }
//
//         public override int GetHashCode()
//         {
//             return m_Location.Hash(m_DesiredType);
//         }
//
//         public override bool Equals(object obj) => Equals(obj as LocationCacheKey);
//         public bool Equals(IOperationCacheKey other) => Equals(other as LocationCacheKey);
//
//         bool Equals(LocationCacheKey other)
//         {
//             if (ReferenceEquals(this, other)) return true;
//             if (ReferenceEquals(other, null)) return false;
//             return LocationUtils.LocationEquals(m_Location, other.m_Location) && Equals(m_DesiredType, other.m_DesiredType);
//         }
//     }
//
//     internal sealed class DependenciesCacheKey : IOperationCacheKey
//     {
//         readonly IList<IResourceLocation> m_Dependencies;
//         readonly int m_DependenciesHash;
//
//         public DependenciesCacheKey(IList<IResourceLocation> dependencies, int dependenciesHash)
//         {
//             m_Dependencies = dependencies;
//             m_DependenciesHash = dependenciesHash;
//         }
//
//         public override int GetHashCode()
//         {
//             return m_DependenciesHash;
//         }
//
//         public override bool Equals(object obj) => Equals(obj as DependenciesCacheKey);
//         public bool Equals(IOperationCacheKey other) => Equals(other as DependenciesCacheKey);
//
//         bool Equals(DependenciesCacheKey other)
//         {
//             if (ReferenceEquals(this, other)) return true;
//             if (ReferenceEquals(other, null)) return false;
//             return LocationUtils.DependenciesEqual(m_Dependencies, other.m_Dependencies);
//         }
//     }
//
//     internal sealed class AsyncOpHandlesCacheKey : IOperationCacheKey
//     {
//         readonly HashSet<AsyncOperationHandle> m_Handles;
//
//         public AsyncOpHandlesCacheKey(IList<AsyncOperationHandle> handles)
//         {
//             m_Handles = new HashSet<AsyncOperationHandle>(handles);
//         }
//
//         public override int GetHashCode()
//         {
//             return m_Handles.GetHashCode();
//         }
//
//         public override bool Equals(object obj) => Equals(obj as AsyncOpHandlesCacheKey);
//         public bool Equals(IOperationCacheKey other) => Equals(other as AsyncOpHandlesCacheKey);
//
//         bool Equals(AsyncOpHandlesCacheKey other)
//         {
//             if (ReferenceEquals(this, other)) return true;
//             if (ReferenceEquals(other, null)) return false;
//             return m_Handles.SetEquals(other.m_Handles);
//         }
//     }
//
//     internal static class LocationUtils
//     {
//         // TODO : Added equality methods here to prevent a minor version bump since we intend to stay on v1.18.x for a while.
//         // Ideally this should have been the Equals() implementation of IResourceLocation
//         public static bool LocationEquals(IResourceLocation loc1, IResourceLocation loc2)
//         {
//             if (ReferenceEquals(loc1, loc2)) return true;
//             if (ReferenceEquals(loc1, null)) return false;
//             if (ReferenceEquals(loc2, null)) return false;
//
//             return (loc1.InternalId.Equals(loc2.InternalId)
//                     && loc1.ProviderId.Equals(loc2.ProviderId)
//                     && loc1.ResourceType.Equals(loc2.ResourceType));
//         }
//
//         public static bool DependenciesEqual(IList<IResourceLocation> deps1, IList<IResourceLocation> deps2)
//         {
//             if (ReferenceEquals(deps1, deps2)) return true;
//             if (ReferenceEquals(deps1, null)) return false;
//             if (ReferenceEquals(deps2, null)) return false;
//             if (deps1.Count != deps2.Count)
//                 return false;
//
//             for (int i = 0; i < deps1.Count; i++)
//             {
//                 if (!LocationEquals(deps1[i], deps2[i]))
//                     return false;
//             }
//
//             return true;
//         }
//     }
//     
//     [System.Flags]
//     internal enum BundleSource
//     {
//         None = 0,
//         Local = 1,
//         Cache = 2,
//         Download = 4
//     }
//     
//     internal class LocationWrapper : IResourceLocation
//     {
//         IResourceLocation m_InternalLocation;
//
//         public LocationWrapper(IResourceLocation location)
//         {
//             m_InternalLocation = location;
//         }
//
//         public string InternalId => m_InternalLocation.InternalId;
//
//         public string ProviderId => m_InternalLocation.ProviderId;
//
//         public IList<IResourceLocation> Dependencies => m_InternalLocation.Dependencies;
//
//         public int DependencyHashCode => m_InternalLocation.DependencyHashCode;
//
//         public bool HasDependencies => m_InternalLocation.HasDependencies;
//
//         public object Data => m_InternalLocation.Data;
//
//         public string PrimaryKey => m_InternalLocation.PrimaryKey;
//
//         public Type ResourceType => m_InternalLocation.ResourceType;
//
//         public int Hash(Type resultType)
//         {
//             return m_InternalLocation.Hash(resultType);
//         }
//     }
//     
//     internal class DownloadOnlyLocation : LocationWrapper
//     {
//         public DownloadOnlyLocation(IResourceLocation location) : base(location)
//         {
//         }
//     }
//
//     /// <summary>
//     /// Provides methods for loading an AssetBundle from a local or remote location.
//     /// </summary>
//     public class AssetBundleResource : IAssetBundleResource, IUpdateReceiver
//     {
//         /// <summary>
//         /// Options for where an AssetBundle can be loaded from.
//         /// </summary>
//         public enum LoadType
//         {
//             /// <summary>
//             /// Cannot determine where the AssetBundle is located.
//             /// </summary>
//             None,
//
//             /// <summary>
//             /// Load the AssetBundle from a local file location.
//             /// </summary>
//             Local,
//
//             /// <summary>
//             /// Download the AssetBundle from a web server.
//             /// </summary>
//             Web
//         }
//
//         AssetBundle m_AssetBundle;
//         AsyncOperation m_RequestOperation;
//         internal WebRequestQueueOperation m_WebRequestQueueOperation;
//         internal ProvideHandle m_ProvideHandle;
//         internal AssetBundleRequestOptions m_Options;
//
//         [NonSerialized]
//         bool m_RequestCompletedCallbackCalled = false;
//
//         int m_Retries;
//         BundleSource m_Source = BundleSource.None;
//         long m_BytesToDownload;
//         long m_DownloadedBytes;
//         bool m_Completed = false;
// #if UNLOAD_BUNDLE_ASYNC
//         AssetBundleUnloadOperation m_UnloadOperation;
// #endif
//         const int k_WaitForWebRequestMainThreadSleep = 1;
//         string m_TransformedInternalId;
//         AssetBundleRequest m_PreloadRequest;
//         bool m_PreloadCompleted = false;
//         ulong m_LastDownloadedByteCount = 0;
//         float m_TimeoutTimer = 0;
//         int m_TimeoutOverFrames = 0;
//         int m_LastFrameCount = -1;
//         float m_TimeSecSinceLastUpdate = 0;
//
//         private bool HasTimedOut => m_TimeoutTimer >= m_Options.Timeout && m_TimeoutOverFrames > 5;
//
//         internal long BytesToDownload
//         {
//             get
//             {
//                 if (m_BytesToDownload == -1)
//                 {
//                     if (m_Options != null)
//                         m_BytesToDownload = m_Options.ComputeSize(m_ProvideHandle.Location, m_ProvideHandle.ResourceManager);
//                     else
//                         m_BytesToDownload = 0;
//                 }
//
//                 return m_BytesToDownload;
//             }
//         }
//
//         internal UnityWebRequest CreateWebRequest(IResourceLocation loc)
//         {
//             var url = m_ProvideHandle.ResourceManager.TransformInternalId(loc);
//             return CreateWebRequest(url);
//         }
//
//         internal UnityWebRequest CreateWebRequest(string url)
//         {
//             string sanitizedUrl = Uri.UnescapeDataString(url);
//
// #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
//             Uri uri = new Uri(sanitizedUrl.Replace(" ", "%20"));
// #else
//             Uri uri = new Uri(Uri.EscapeUriString(sanitizedUrl));
// #endif
//
//             if (m_Options == null)
//             {
//                 m_Source = BundleSource.Download;
// #if ENABLE_ADDRESSABLE_PROFILER
//                 AddBundleToProfiler(Profiling.ContentStatus.Downloading, m_Source);
// #endif
//                 return UnityWebRequestAssetBundle.GetAssetBundle(uri);
//             }
//
//             UnityWebRequest webRequest;
//             if (!string.IsNullOrEmpty(m_Options.Hash))
//             {
//                 CachedAssetBundle cachedBundle = new CachedAssetBundle(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
// #if ENABLE_CACHING
//                 bool cached = Caching.IsVersionCached(cachedBundle);
//                 m_Source = cached ? BundleSource.Cache : BundleSource.Download;
//                 if (m_Options.UseCrcForCachedBundle || m_Source == BundleSource.Download)
//                     webRequest = UnityWebRequestAssetBundle.GetAssetBundle(uri, cachedBundle, m_Options.Crc);
//                 else
//                     webRequest = UnityWebRequestAssetBundle.GetAssetBundle(uri, cachedBundle);
// #else
//                 webRequest = UnityWebRequestAssetBundle.GetAssetBundle(uri, cachedBundle, m_Options.Crc);
// #endif
//             }
//             else
//             {
//                 m_Source = BundleSource.Download;
//                 webRequest = UnityWebRequestAssetBundle.GetAssetBundle(uri, m_Options.Crc);
//             }
//
//             if (m_Options.RedirectLimit > 0)
//                 webRequest.redirectLimit = m_Options.RedirectLimit;
//             if (m_ProvideHandle.ResourceManager.CertificateHandlerInstance != null)
//             {
//                 webRequest.certificateHandler = m_ProvideHandle.ResourceManager.CertificateHandlerInstance;
//                 webRequest.disposeCertificateHandlerOnDispose = false;
//             }
//
//             m_ProvideHandle.ResourceManager.WebRequestOverride?.Invoke(webRequest);
//             return webRequest;
//         }
//
//         /// <summary>
//         /// Creates a request for loading all assets from an AssetBundle.
//         /// </summary>
//         /// <returns>Returns the request.</returns>
//         public AssetBundleRequest GetAssetPreloadRequest()
//         {
//             if (m_PreloadCompleted || GetAssetBundle() == null)
//                 return null;
//
//             if (m_Options.AssetLoadMode == AssetLoadMode.AllPackedAssetsAndDependencies)
//             {
// #if !UNITY_2021_1_OR_NEWER
//                 if (AsyncOperationHandle.IsWaitingForCompletion)
//                 {
//                     m_AssetBundle.LoadAllAssets();
//                     m_PreloadCompleted = true;
//                     return null;
//                 }
// #endif
//                 if (m_PreloadRequest == null)
//                 {
//                     m_PreloadRequest = m_AssetBundle.LoadAllAssetsAsync();
//                     m_PreloadRequest.completed += operation => m_PreloadCompleted = true;
//                 }
//
//                 return m_PreloadRequest;
//             }
//
//             return null;
//         }
//
//         float PercentComplete()
//         {
//             return m_RequestOperation != null ? m_RequestOperation.progress : 0.0f;
//         }
//
//         DownloadStatus GetDownloadStatus()
//         {
//             if (m_Options == null)
//                 return default;
//             var status = new DownloadStatus() {TotalBytes = BytesToDownload, IsDone = PercentComplete() >= 1f};
//             if (BytesToDownload > 0)
//             {
//                 if (m_WebRequestQueueOperation != null && string.IsNullOrEmpty(m_WebRequestQueueOperation.WebRequest.error))
//                     m_DownloadedBytes = (long)(m_WebRequestQueueOperation.WebRequest.downloadedBytes);
//                 else if (m_RequestOperation != null && m_RequestOperation is UnityWebRequestAsyncOperation operation && string.IsNullOrEmpty(operation.webRequest.error))
//                     m_DownloadedBytes = (long)operation.webRequest.downloadedBytes;
//             }
//
//             status.DownloadedBytes = m_DownloadedBytes;
//             return status;
//         }
//
//         /// <summary>
//         /// Get the asset bundle object managed by this resource.  This call may force the bundle to load if not already loaded.
//         /// </summary>
//         /// <returns>The asset bundle.</returns>
//         public AssetBundle GetAssetBundle()
//         {
//             bool isValid = (bool?)typeof(UnityEngine.ResourceManagement.ResourceProviders.ProvideHandle)
//     .GetProperty("IsValid", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
//     ?.GetValue(m_ProvideHandle) == true;
//             if (isValid)
//             {
//                 Debug.Assert(!(m_ProvideHandle.Location is DownloadOnlyLocation), "GetAssetBundle does not return a value when an AssetBundle is download only.");
//             }
//             return m_AssetBundle;
//         }
//
// #if ENABLE_ADDRESSABLE_PROFILER
//         private void AddBundleToProfiler(Profiling.ContentStatus status, BundleSource source)
//         {
//             if (!Profiler.enabled)
//                 return;
//             if (!m_ProvideHandle.IsValid)
//                 return;
//
//             if (status == Profiling.ContentStatus.Active && m_AssetBundle == null) // is this going to suggest load only are released?
//                 Profiling.ProfilerRuntime.BundleReleased(m_Options.BundleName);
//             else
//                 Profiling.ProfilerRuntime.AddBundleOperation(m_ProvideHandle, m_Options, status, source);
//         }
//
//         private void RemoveBundleFromProfiler()
//         {
//             if (m_Options == null)
//                 return;
//             Profiling.ProfilerRuntime.BundleReleased(m_Options.BundleName);
//         }
//
// #endif
//
// #if UNLOAD_BUNDLE_ASYNC
//         void OnUnloadOperationComplete(AsyncOperation op)
//         {
//             m_UnloadOperation = null;
//             BeginOperation();
//         }
//
// #endif
//
// #if UNLOAD_BUNDLE_ASYNC
//         /// <summary>
//         /// Stores AssetBundle loading information, starts loading the bundle.
//         /// </summary>
//         /// <param name="provideHandle">The container for AssetBundle loading information.</param>
//         /// <param name="unloadOp">The async operation for unloading the AssetBundle.</param>
//         public void Start(ProvideHandle provideHandle, AssetBundleUnloadOperation unloadOp)
// #else
//         /// <summary>
//         /// Stores AssetBundle loading information, starts loading the bundle.
//         /// </summary>
//         /// <param name="provideHandle">The container for information regarding loading the AssetBundle.</param>
//         public void Start(ProvideHandle provideHandle)
// #endif
//         {
//             m_Retries = 0;
//             m_AssetBundle = null;
//             m_RequestOperation = null;
//             m_ProvideHandle = provideHandle;
//             m_Options = m_ProvideHandle.Location.Data as AssetBundleRequestOptions;
//             m_BytesToDownload = -1;
//             m_ProvideHandle.SetProgressCallback(PercentComplete);
//             m_ProvideHandle.SetDownloadProgressCallbacks(GetDownloadStatus);
//             m_ProvideHandle.SetWaitForCompletionCallback(WaitForCompletionHandler);
// #if UNLOAD_BUNDLE_ASYNC
//             m_UnloadOperation = unloadOp;
//             if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
//                 m_UnloadOperation.completed += OnUnloadOperationComplete;
//             else
// #endif
//             BeginOperation();
//         }
//
//         private bool WaitForCompletionHandler()
//         {
// #if UNLOAD_BUNDLE_ASYNC
//             if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
//             {
//                 m_UnloadOperation.completed -= OnUnloadOperationComplete;
//                 m_UnloadOperation.WaitForCompletion();
//                 m_UnloadOperation = null;
//                 BeginOperation();
//             }
// #endif
//
//             if (m_RequestOperation == null)
//             {
//                 if (m_WebRequestQueueOperation == null)
//                     return false;
//                 else
//                     WebRequestQueue.WaitForRequestToBeActive(m_WebRequestQueueOperation, k_WaitForWebRequestMainThreadSleep);
//             }
//
//             //We don't want to wait for request op to complete if it's a LoadFromFileAsync. Only UWR will complete in a tight loop like this.
//             if (m_RequestOperation is UnityWebRequestAsyncOperation op)
//             {
//                 while (!UnityWebRequestUtilities.IsAssetBundleDownloaded(op))
//                     System.Threading.Thread.Sleep(k_WaitForWebRequestMainThreadSleep);
// #if ENABLE_ASYNC_ASSETBUNDLE_UWR
//                 if (m_Source == BundleSource.Cache)
//                 {
//                     var downloadHandler = (DownloadHandlerAssetBundle)op?.webRequest?.downloadHandler;
//                     if (downloadHandler.autoLoadAssetBundle)
//                         m_AssetBundle = downloadHandler.assetBundle;
//                 }
// #endif
//                 // WebRequestQueue.DequeueRequest(op);
//
//                 if (!m_RequestCompletedCallbackCalled)
//                 {
//                     m_RequestOperation.completed -= WebRequestOperationCompleted;
//                     WebRequestOperationCompleted(m_RequestOperation);
//                 }
//             }
//
//             if (!m_Completed && m_Source == BundleSource.Local)
//             {
//                 // we don't have to check for done with local files as calling
//                 // m_requestOperation.assetBundle is blocking and will wait for the file to load
//                 if (!m_RequestCompletedCallbackCalled)
//                 {
//                     m_RequestOperation.completed -= LocalRequestOperationCompleted;
//                     LocalRequestOperationCompleted(m_RequestOperation);
//                 }
//             }
//
//             if (!m_Completed && m_RequestOperation.isDone)
//             {
//                 m_ProvideHandle.Complete(this, m_AssetBundle != null, null);
//                 m_Completed = true;
//             }
//
//             return m_Completed;
//         }
//
//         void AddCallbackInvokeIfDone(AsyncOperation operation, Action<AsyncOperation> callback)
//         {
//             if (operation.isDone)
//                 callback(operation);
//             else
//                 operation.completed += callback;
//         }
//
//         /// <summary>
//         /// Determines where an AssetBundle can be loaded from.
//         /// </summary>
//         /// <param name="handle">The container for AssetBundle loading information.</param>
//         /// <param name="loadType">Specifies where an AssetBundle can be loaded from.</param>
//         /// <param name="path">The file path or url where the AssetBundle is located.</param>
//         public static void GetLoadInfo(ProvideHandle handle, out LoadType loadType, out string path)
//         {
//             GetLoadInfo(handle.Location, handle.ResourceManager, out loadType, out path);
//         }
//
//         internal static void GetLoadInfo(IResourceLocation location, ResourceManager resourceManager, out LoadType loadType, out string path)
//         {
//             var options = location?.Data as AssetBundleRequestOptions;
//             if (options == null)
//             {
//                 loadType = LoadType.None;
//                 path = null;
//                 return;
//             }
//
//             path = resourceManager.TransformInternalId(location);
//             if (Application.platform == RuntimePlatform.Android && path.StartsWith("jar:", StringComparison.Ordinal))
//                 loadType = options.UseUnityWebRequestForLocalBundles ? LoadType.Web : LoadType.Local;
//             else if (ResourceManagerConfig.ShouldPathUseWebRequest(path))
//                 loadType = LoadType.Web;
//             else if (options.UseUnityWebRequestForLocalBundles)
//             {
//                 path = "file:///" + Path.GetFullPath(path);
//                 loadType = LoadType.Web;
//             }
//             else
//                 loadType = LoadType.Local;
//
//             if (loadType == LoadType.Web)
//                 path = path.Replace('\\', '/');
//         }
//
//         private void BeginOperation()
//         {
//             // retrying a failed request will call BeginOperation multiple times. Any member variables
//             // should be reset at the beginning of the operation
//             m_DownloadedBytes = 0;
//             m_RequestCompletedCallbackCalled = false;
//             GetLoadInfo(m_ProvideHandle, out LoadType loadType, out m_TransformedInternalId);
//             
//             if (loadType == LoadType.Local)
//             {
//                 //download only bundles loads should not load local bundles
//                 if (m_ProvideHandle.Location is DownloadOnlyLocation)
//                 {
//                     m_Source = BundleSource.Local;
//                     m_RequestOperation = null;
//                     m_ProvideHandle.Complete<AssetBundleResource>(null, true, null);
//                     m_Completed = true;
//                 }
//                 else
//                 {
//                     LoadLocalBundle();
//                 }
//                 return;
//             }
//
//             if (loadType == LoadType.Web)
//             {
//                 m_WebRequestQueueOperation = EnqueueWebRequest(m_TransformedInternalId);
//                 AddBeginWebRequestHandler(m_WebRequestQueueOperation);
//                 return;
//             }
//
//             m_Source = BundleSource.None;
//             m_RequestOperation = null;
//             m_ProvideHandle.Complete<AssetBundleResource>(null, false,
//                 new RemoteProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId), m_ProvideHandle.Location));
//             m_Completed = true;
//         }
//
//         private void LoadLocalBundle()
//         {
//             m_Source = BundleSource.Local;
// #if !UNITY_2021_1_OR_NEWER
//             if (AsyncOperationHandle.IsWaitingForCompletion)
//                 CompleteBundleLoad(AssetBundle.LoadFromFile(m_TransformedInternalId, m_Options == null ? 0 : m_Options.Crc));
//             else
// #endif
//             {
//                 m_RequestOperation = AssetBundle.LoadFromFileAsync(m_TransformedInternalId, m_Options == null ? 0 : m_Options.Crc);
// #if ENABLE_ADDRESSABLE_PROFILER
//                 AddBundleToProfiler(Profiling.ContentStatus.Loading, m_Source);
// #endif
//                 AddCallbackInvokeIfDone(m_RequestOperation, LocalRequestOperationCompleted);
//             }
//         }
//
//         internal WebRequestQueueOperation EnqueueWebRequest(string internalId)
//         {
//             var req = CreateWebRequest(internalId);
// #if ENABLE_ASYNC_ASSETBUNDLE_UWR
//             ((DownloadHandlerAssetBundle)req.downloadHandler).autoLoadAssetBundle = !(m_ProvideHandle.Location is DownloadOnlyLocation);
// #endif
//             req.disposeDownloadHandlerOnDispose = false;
//
//             return WebRequestQueue.QueueRequest(req);
//         }
//
//         internal void AddBeginWebRequestHandler(WebRequestQueueOperation webRequestQueueOperation)
//         {
//             if (webRequestQueueOperation.IsDone)
//             {
//                 BeginWebRequestOperation(webRequestQueueOperation.Result);
//             }
//             else
//             {
// #if ENABLE_ADDRESSABLE_PROFILER
//                 AddBundleToProfiler(Profiling.ContentStatus.Queue, m_Source);
// #endif
//                 webRequestQueueOperation.OnComplete += asyncOp => BeginWebRequestOperation(asyncOp);
//             }
//         }
//
//         private void BeginWebRequestOperation(AsyncOperation asyncOp)
//         {
//             m_TimeoutTimer = 0;
//             m_TimeoutOverFrames = 0;
//             m_LastDownloadedByteCount = 0;
//             m_RequestOperation = asyncOp;
//             if (m_RequestOperation == null || m_RequestOperation.isDone)
//                 WebRequestOperationCompleted(m_RequestOperation);
//             else
//             {
//                 if (m_Options.Timeout > 0)
//                     m_ProvideHandle.ResourceManager.AddUpdateReceiver(this);
// #if ENABLE_ADDRESSABLE_PROFILER
//                 AddBundleToProfiler(m_Source == BundleSource.Cache ? Profiling.ContentStatus.Loading : Profiling.ContentStatus.Downloading, m_Source);
// #endif
//                 m_RequestOperation.completed += WebRequestOperationCompleted;
//             }
//         }
//
//         /// <inheritdoc/>
//         public void Update(float unscaledDeltaTime)
//         {
//             if (m_RequestOperation != null && m_RequestOperation is UnityWebRequestAsyncOperation operation && !operation.isDone)
//             {
//                 if (m_LastDownloadedByteCount != operation.webRequest.downloadedBytes)
//                 {
//                     m_TimeoutTimer = 0;
//                     m_TimeoutOverFrames = 0;
//                     m_LastDownloadedByteCount = operation.webRequest.downloadedBytes;
//
//                     m_LastFrameCount = -1;
//                     m_TimeSecSinceLastUpdate = 0;
//                 }
//                 else
//                 {
//                     float updateTime = unscaledDeltaTime;
//                     if (m_LastFrameCount == Time.frameCount)
//                     {
//                         updateTime = Time.realtimeSinceStartup - m_TimeSecSinceLastUpdate;
//                     }
//
//                     m_TimeoutTimer += updateTime;
//                     if (HasTimedOut)
//                         operation.webRequest.Abort();
//                     m_TimeoutOverFrames++;
//
//                     m_LastFrameCount = Time.frameCount;
//                     m_TimeSecSinceLastUpdate = Time.realtimeSinceStartup;
//                 }
//             }
//         }
//
//         private void LocalRequestOperationCompleted(AsyncOperation op)
//         {
//             if (m_RequestCompletedCallbackCalled)
//             {
//                 return;
//             }
//
//             m_RequestCompletedCallbackCalled = true;
//             CompleteBundleLoad((op as AssetBundleCreateRequest).assetBundle);
//         }
//
//         private void CompleteBundleLoad(AssetBundle bundle)
//         {
//             m_AssetBundle = bundle;
// #if ENABLE_ADDRESSABLE_PROFILER
//             AddBundleToProfiler(Profiling.ContentStatus.Active, m_Source);
// #endif
//             if (m_AssetBundle != null)
//                 m_ProvideHandle.Complete(this, true, null);
//             else
//                 m_ProvideHandle.Complete<AssetBundleResource>(null, false,
//                     new RemoteProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId), m_ProvideHandle.Location));
//             m_Completed = true;
//         }
//
//         private void WebRequestOperationCompleted(AsyncOperation op)
//         {
//             if (m_RequestCompletedCallbackCalled)
//                 return;
//
//             m_RequestCompletedCallbackCalled = true;
//
//             if (m_Options.Timeout > 0)
//                 m_ProvideHandle.ResourceManager.RemoveUpdateReciever(this);
//
//             UnityWebRequestAsyncOperation remoteReq = op as UnityWebRequestAsyncOperation;
//             var webReq = remoteReq?.webRequest;
//             var downloadHandler = webReq?.downloadHandler as DownloadHandlerAssetBundle;
//             UnityWebRequestResult uwrResult = null;
//             if (webReq != null && !UnityWebRequestUtilities.RequestHasErrors(webReq, out uwrResult))
//             {
//                 if (!m_Completed)
//                 {
//                     downloadHandler.Dispose();
//                     downloadHandler = null;
//                     
//                     if (!(m_ProvideHandle.Location is DownloadOnlyLocation))
//                     {
//                         // this loads the bundle into memory which we don't want to do with download only bundles
//                         var key = m_ProvideHandle.Location.PrimaryKey;
//                         var loading = AssetBundle.LoadFromFileAsync(Path.Combine(Caching.GetCacheAt(0).path, key));
//                         loading.completed += (op) =>
//                         {
//                             CompleteBundleLoad((op as AssetBundleCreateRequest).assetBundle);
//                         };
//                     }
//                     else
//                     {
// #if ENABLE_ADDRESSABLE_PROFILER
//                     AddBundleToProfiler(Profiling.ContentStatus.Active, m_Source);
// #endif
//                         m_ProvideHandle.Complete(this, true, null);
//                         m_Completed = true;
//                     }
//                 }
// #if ENABLE_CACHING
//                 if (!string.IsNullOrEmpty(m_Options.Hash) && m_Options.ClearOtherCachedVersionsWhenLoaded)
//                     Caching.ClearOtherCachedVersions(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
// #endif
//             }
//             else
//             {
//                 if (HasTimedOut)
//                     uwrResult.Error = "Request timeout";
//                 webReq = m_WebRequestQueueOperation.WebRequest;
//                 if (uwrResult == null)
//                     uwrResult = new UnityWebRequestResult(m_WebRequestQueueOperation.WebRequest);
//
//                 downloadHandler = webReq.downloadHandler as DownloadHandlerAssetBundle;
//                 downloadHandler.Dispose();
//                 downloadHandler = null;
//                 bool forcedRetry = false;
//                 string message = $"Web request failed, retrying ({m_Retries}/{m_Options.RetryCount})...\n{uwrResult}";
// #if ENABLE_CACHING
//                 if (!string.IsNullOrEmpty(m_Options.Hash))
//                 {
// #if ENABLE_ADDRESSABLE_PROFILER
//                     if (m_Source == BundleSource.Cache)
// #endif
//                     {
//                         message = $"Web request failed to load from cache. The cached AssetBundle will be cleared from the cache and re-downloaded. Retrying...\n{uwrResult}";
//                         Caching.ClearCachedVersion(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
//                         // When attempted to load from cache we always retry on first attempt and failed
//                         if (m_Retries == 0 && uwrResult.ShouldRetryDownloadError())
//                         {
//                             Debug.LogFormat(message);
//                             BeginOperation();
//                             m_Retries++; //Will prevent us from entering an infinite loop of retrying if retry count is 0
//                             forcedRetry = true;
//                         }
//                     }
//                 }
// #endif
//                 if (!forcedRetry)
//                 {
//                     if (m_Retries < m_Options.RetryCount && uwrResult.ShouldRetryDownloadError())
//                     {
//                         m_Retries++;
//                         Debug.LogFormat(message);
//                         BeginOperation();
//                     }
//                     else
//                     {
//                         var exception = new RemoteProviderException($"Unable to load asset bundle from : {webReq.url}", m_ProvideHandle.Location, uwrResult);
//                         m_ProvideHandle.Complete<AssetBundleResource>(null, false, exception);
//                         m_Completed = true;
// #if ENABLE_ADDRESSABLE_PROFILER
//                         RemoveBundleFromProfiler();
// #endif
//                     }
//                 }
//             }
//
//             webReq.Dispose();
//         }
//
// #if UNLOAD_BUNDLE_ASYNC
//         /// <summary>
//         /// Starts an async operation that unloads all resources associated with the AssetBundle.
//         /// </summary>
//         /// <param name="unloadOp">The async operation.</param>
//         /// <returns>Returns true if the async operation object is valid.</returns>
//         public bool Unload(out AssetBundleUnloadOperation unloadOp)
// #else
//         /// <summary>
//         /// Unloads all resources associated with the AssetBundle.
//         /// </summary>
//         public void Unload()
// #endif
//         {
// #if UNLOAD_BUNDLE_ASYNC
//             unloadOp = null;
//             if (m_AssetBundle != null)
//             {
//                 unloadOp = m_AssetBundle.UnloadAsync(true);
//                 m_AssetBundle = null;
//             }
// #else
//             if (m_AssetBundle != null)
//             {
//                 m_AssetBundle.Unload(true);
//                 m_AssetBundle = null;
//             }
// #endif
//             m_RequestOperation = null;
// #if ENABLE_ADDRESSABLE_PROFILER
//             RemoveBundleFromProfiler();
// #endif
// #if UNLOAD_BUNDLE_ASYNC
//             return unloadOp != null;
// #endif
//         }
//     }
//
//     /// <summary>
//     /// IResourceProvider for asset bundles.  Loads bundles via UnityWebRequestAssetBundle API if the internalId starts with "http".  If not, it will load the bundle via AssetBundle.LoadFromFileAsync.
//     /// </summary>
//     [DisplayName(nameof(BackgroundDownloadResourceProvider))]
//     public class BackgroundDownloadResourceProvider : ResourceProviderBase
//     {
// #if UNLOAD_BUNDLE_ASYNC
//         internal static Dictionary<string, AssetBundleUnloadOperation> m_UnloadingBundles = new Dictionary<string, AssetBundleUnloadOperation>();
//
//         [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
//         private static void Init()
//         {
//             m_UnloadingBundles = new Dictionary<string, AssetBundleUnloadOperation>();
//         }
//
//         /// <summary>
//         /// Stores async operations that unload the requested AssetBundles.
//         /// </summary>
//         protected internal static Dictionary<string, AssetBundleUnloadOperation> UnloadingBundles
//         {
//             get { return m_UnloadingBundles; }
//             internal set { m_UnloadingBundles = value; }
//         }
//
//         internal static int UnloadingAssetBundleCount => m_UnloadingBundles.Count;
//         internal static int AssetBundleCount => AssetBundle.GetAllLoadedAssetBundles().Count() - UnloadingAssetBundleCount;
//         internal static void WaitForAllUnloadingBundlesToComplete()
//         {
//             if (UnloadingAssetBundleCount > 0)
//             {
//                 var bundles = m_UnloadingBundles.Values.ToArray();
//                 foreach (var b in bundles)
//                     b.WaitForCompletion();
//             }
//         }
//
// #else
//         internal static void WaitForAllUnloadingBundlesToComplete()
//         {
//         }
//
// #endif
//
//         /// <inheritdoc/>
//         public override void Provide(ProvideHandle providerInterface)
//         {
// #if UNLOAD_BUNDLE_ASYNC
//             if (m_UnloadingBundles.TryGetValue(providerInterface.Location.InternalId, out var unloadOp))
//             {
//                 if (unloadOp.isDone)
//                     unloadOp = null;
//             }
//             new AssetBundleResource().Start(providerInterface, unloadOp);
// #else
//             new AssetBundleResource().Start(providerInterface);
// #endif
//         }
//
//         /// <inheritdoc/>
//         public override Type GetDefaultType(IResourceLocation location)
//         {
//             return typeof(IAssetBundleResource);
//         }
//
//         /// <summary>
//         /// Releases the asset bundle via AssetBundle.Unload(true).
//         /// </summary>
//         /// <param name="location">The location of the asset to release</param>
//         /// <param name="asset">The asset in question</param>
//         public override void Release(IResourceLocation location, object asset)
//         {
//             if (location == null)
//                 throw new ArgumentNullException("location");
//             if (asset == null)
//             {
//                 if(!(location is DownloadOnlyLocation))
//                     Debug.LogWarningFormat("Releasing null asset bundle from location {0}.  This is an indication that the bundle failed to load.", location);
//                 return;
//             }
//
//             var bundle = asset as AssetBundleResource;
//             if (bundle != null)
//             {
// #if UNLOAD_BUNDLE_ASYNC
//                 if (bundle.Unload(out var unloadOp))
//                 {
//                     m_UnloadingBundles.Add(location.InternalId, unloadOp);
//                     unloadOp.completed += op => m_UnloadingBundles.Remove(location.InternalId);
//                 }
// #else
//                 bundle.Unload();
// #endif
//                 return;
//             }
//         }
//
//         internal virtual IOperationCacheKey CreateCacheKeyForLocation(ResourceManager rm, IResourceLocation location, Type desiredType)
//         {
//             //We need to transform the ID first
//             //so we don't try and load the same bundle twice if the user is manipulating the path at runtime.
//             return new IdCacheKey(location.GetType(), rm.TransformInternalId(location));
//         }
//     }
// }
