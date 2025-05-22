using System.Collections;
using UnityEngine;

/// <summary>
/// Helper class to start and stop coroutines from non-MonoBehaviour classes.
/// Attach this to a persistent GameObject in your scene.
/// </summary>
public class CoroutineStarter : MonoBehaviour
{
    private static CoroutineStarter m_Instance;

    public static CoroutineStarter Instance
    {
        get
        {
            if (m_Instance == null)
            {
                GameObject go = new GameObject("CoroutineStarter");
                m_Instance = go.AddComponent<CoroutineStarter>();
                DontDestroyOnLoad(go); // Ensure it persists across scene loads
            }
            return m_Instance;
        }
    }

    public static Coroutine StartCoroutineStatic(IEnumerator coroutine)
    {
        return Instance.StartCoroutine(coroutine);
    }

    public static void StopCoroutineStatic(Coroutine coroutine)
    {
        Instance.StopCoroutine(coroutine);
    }
}