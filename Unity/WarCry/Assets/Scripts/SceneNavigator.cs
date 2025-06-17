using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides utility methods for navigating and interacting with GameObjects
/// in the Unity scene hierarchy.
/// </summary>
public static class SceneNavigator
{
    /// <summary>
    /// Finds a GameObject in the scene hierarchy based on a provided path.
    /// The path should be a slash-separated string representing the hierarchy
    /// of GameObjects, starting from a root GameObject.
    /// </summary>
    /// <param name="path">The path of the GameObject in the scene hierarchy.
    /// Each part of the path corresponds to a GameObject's name in the hierarchy.</param>
    /// <returns>The GameObject at the specified path if found; otherwise, null.</returns>
    public static GameObject FindGameObjectAtPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"[{DebugUtils.GetCallerInfo()}] Path is empty");
            return null;
        }

        string[] pathParts = path.Split('/');

        // Find the root object
        GameObject rootObject = null;
        foreach (GameObject root in SceneManager.GetActiveScene()
                     .GetRootGameObjects())
        {
            if (root.name == pathParts[0])
            {
                rootObject = root;
                break;
            }
        }

        if (rootObject == null)
        {
            Debug.LogError($"[{DebugUtils.GetCallerInfo()}] Object not found: {path}");
            return rootObject;
        }

        // Navigate through the path
        Transform current = rootObject.transform;
        for (int i = 1; i < pathParts.Length; i++)
        {
            Transform child = current.Find(pathParts[i]);
            if (child == null)
            {
                Debug.LogError($"[{DebugUtils.GetCallerInfo()}] Object not found: {path}");
                return null;
            }

            current = child;
        }

        return current.gameObject;
    }

    /// <summary>
    /// Retrieves a component of a specified type from a GameObject located at the given path
    /// in the scene hierarchy. The path should be a slash-separated string representing
    /// the hierarchy of GameObjects, starting from a root GameObject.
    /// </summary>
    /// <param name="path">The path of the GameObject in the scene hierarchy.
    /// Each part of the path corresponds to a GameObject's name in the hierarchy.</param>
    /// <typeparam name="T">The type of the component to retrieve.</typeparam>
    /// <returns>The component of the specified type if found; otherwise, null.</returns>
    public static T FindComponentAtPath<T>(string path) where T : Component
    {
        GameObject obj = FindGameObjectAtPath(path);
        if (obj != null)
        {
            return obj.GetComponent<T>();
        }

        Debug.LogError($"[{DebugUtils.GetCallerInfo()}] Component not found: {path}");
        return null;
    }

    /// <summary>
    /// Retrieves the path of a GameObject within the scene hierarchy as a slash-separated string.
    /// The path starts with the scene name, followed by the root GameObject and includes all parent 
    /// GameObjects up to the specified GameObject.
    /// </summary>
    /// <param name="obj">The GameObject whose path should be retrieved.</param>
    /// <returns>A string representing the GameObject's path within the scene hierarchy, 
    /// prefixed with the scene name. Returns an empty string if the GameObject is null.</returns>
    /// <remarks>
    /// If the object is null, then it returns [null gameObject].
    /// If the scene is invalid, then it is prefixed with [invalid scene].
    /// </remarks>
    public static string RetrievePath(GameObject obj)
    {
        if (obj == null)
        {
            return "[null gameObject]";
        }

        List<string> pathParts = new List<string>();
        Transform current = obj.transform;

        while (current != null)
        {
            pathParts.Add(current.name);
            current = current.parent;
        }

        pathParts.Reverse();
    
        string sceneName = obj.scene.IsValid() ? obj.scene.name : "[invalid scene]";
        return $"{sceneName}/{string.Join("/", pathParts)}";
    }

    [Obsolete("This is a deprecated method. Use FindGameObjectByPath instead.")]
    public static GameObject FindByPath(string path) => FindGameObjectAtPath(path);
    [Obsolete("This is a deprecated method. Use FindComponentAtPath instead.")]
    public static T GetComponentByPath<T>(string path) where T : Component =>
        FindComponentAtPath<T>(path);
    [Obsolete("This is a deprecated method. Use RetrievePath instead")]
    public static string GetPath(GameObject obj) => RetrievePath(obj);
}