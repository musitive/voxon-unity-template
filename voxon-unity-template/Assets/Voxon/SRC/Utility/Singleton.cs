using UnityEngine;
// ReSharper disable CheckNamespace


/// <summary>
/// Be aware this will not prevent a non singleton constructor
///   such as `T myT = new T();`
/// To prevent that, add `protected T () {}` to your singleton class.
/// 
/// As a note, this is made as MonoBehaviour because we need Coroutines.
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static object _lock = new object();

    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                Debug.LogWarning("[Singleton] Instance '" + typeof(T) +
                    "' already destroyed on application quit." +
                    " Won't create again - returning null.");
                return null;
            }

            lock (_lock)
            {


                if (_instance != null) return _instance;

#if UNITY_6000_0_OR_NEWER
                  _instance = (T)Object.FindFirstObjectByType(typeof(T));
                if (Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length > 1)
#else
                _instance = (T)FindObjectOfType(typeof(T));


                if (FindObjectsOfType(typeof(T)).Length > 1)
#endif
                {
                    Debug.LogWarning("Found more than one instance of " + typeof(T) + ". Attempting to return the correct instance...");

#if UNITY_6000_0_OR_NEWER


                  
                    var instances = Object.FindObjectsByType(typeof(Singleton<T>), FindObjectsSortMode.None);

#else
                    var instances = FindObjectsOfType(typeof(Singleton<T>));
#endif

                    foreach (var instance in instances)
                    {
                        if (instance != null)
                        {
                            Debug.Log("Found the non null instance, so returning it");
                            _instance = (T)instance;
                            return _instance;
                        }
                    }
                }

                if (_instance == null)
                {
                    GameObject singleton = new GameObject();
                    _instance = singleton.AddComponent<T>();
                    singleton.name = typeof(T).ToString();
                    singleton.hideFlags = HideFlags.HideInHierarchy;

#if UNITY_EDITOR
                   // DontDestroyOnLoad(singleton);
#else
                   DontDestroyOnLoad(singleton);
#endif

                /*
                    Debug.Log("[Singleton] An instance of " + typeof(T) +
                            " is needed in the scene, so '" + singleton +
                            "' was created with DontDestroyOnLoad.");

                */
                }
                else
                {
#if UNITY_6000_0_OR_NEWER
                    if (Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length > 1)
#else
                    if (FindObjectsOfType(typeof(T)).Length > 1)
#endif 
                    {

#if UNITY_6000_0_OR_NEWER
                        var instances = Object.FindObjectsByType(typeof(Singleton<T>), FindObjectsSortMode.None);
#else
                        var instances = FindObjectsOfType(typeof(Singleton<T>));
#endif
                        int i = 0;
                        foreach (var instance in instances)
                        {
                            Debug.Log($"searching instances {i} ");
                            i++;

                            if (instance != null)
                            {
                                Debug.Log("found a non null instance");
                                _instance = (T)instance;
                                return _instance;
                            }
                        }



                    }


                }

                return _instance;
            }
        }
    }
    private static bool _applicationIsQuitting;

    /// <summary>
    /// When our parent object is destroyed (In Voxon's case this will be the capture volume)
    /// We'll wipe our instance so that if we get recalled (due to a new capture volume) it will
    /// force a refresh of the singleton.
    /// </summary>
    public void OnDestroy()
    {
        _instance = null;
    }

    /// <summary>
    /// When Unity quits, it destroys objects in a random order.
    /// In principle, a Singleton is only destroyed when application quits.
    /// If any script calls Instance after it have been destroyed, 
    ///   it will create a buggy ghost object that will stay on the Editor scene
    ///   even after stopping playing the Application. Really bad!
    /// So, this was made to be sure we're not creating that buggy ghost object.
    /// </summary>
    public void OnApplicationQuit()
    {
#if UNITY_EDITOR
#else
            _applicationIsQuitting = true;
#endif
    }
}
