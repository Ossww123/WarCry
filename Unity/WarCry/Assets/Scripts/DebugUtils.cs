using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Provides utility methods for debugging Unity game objects and runtime information, including
/// hierarchical structure visualization and method call tracing capabilities. Use this class
/// for development-time debugging and logging of game object relationships and execution flow.
/// </summary>
public class DebugUtils
{
    /// <summary>
    /// Generates a formatted string representation of a GameObject's transform hierarchy,
    /// showing the complete parent-child relationship tree with position and rotation information.
    /// </summary>
    /// <param name="rootTransform">The Transform component of the root GameObject whose hierarchy will be displayed.</param>
    /// <returns>A multi-line string where each line represents an object in the hierarchy, 
    /// indented by two spaces per level of depth. Each line includes the object's name, 
    /// local position, and local rotation (in Euler angles).</returns>
    public static String FormatHierarchyTree(Transform rootTransform)
    {
        StringBuilder sb = new();
        Stack<(Transform, Int32)> hierarchyStack = new();

        // Start with the root at level 0
        hierarchyStack.Push((rootTransform, 0));

        while (hierarchyStack.Count > 0)
        {
            var (transform, level) = hierarchyStack.Pop();

            // Create the indent based on level
            var indent = new String(' ', level * 2);

            // Add this transform's info
            sb.AppendLine(
                $"{indent}{transform.name}: localPos={transform.localPosition}, localRot={transform.localRotation.eulerAngles}");

            // Queue all children with increased level
            foreach (Transform child in transform)
            {
                hierarchyStack.Push((child, level + 1));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Retrieves information about the calling method using stack trace inspection.
    /// Useful for debugging and logging to identify which method initiated a call.
    /// </summary>
    /// <returns>A string in the format "{ClassName}.{MethodName}" representing the immediate caller 
    /// of the method where this is used. Returns "[unknown]" if caller information cannot be determined.</returns>
    /// <remarks>
    /// This method looks one level up in the call stack, so it will return information
    /// about the method that directly called ResolveCallerMethod().
    /// </remarks>
    public static String ResolveCallerMethod()
    {
        try
        {
            System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
            var method = frame.GetMethod();

            if (method == null)
            {
                return "[unknown method]";
            }

            if (method.DeclaringType == null)
            {
                return $"[unknown class].{method.Name}";
            }

            return $"{method.DeclaringType.Name}.{method.Name}";
        }
        catch (Exception ex)
        {
            return "[error resolving caller]";
        }
    }

    /// <summary>
    /// Retrieves a string combining the full hierarchy path of a specified GameObject and the caller's method information.
    /// Useful for debugging purposes to trace objects and method usage in the game.
    /// </summary>
    /// <param name="gameObject">The GameObject whose path in the hierarchy is to be included in the returned string.</param>
    /// <returns>A string containing the GameObject's hierarchy path and the name of the calling method, formatted as "HierarchyPath/CallerInfo".</returns>
    public static String CombineObjectPathAndCallerMethod(GameObject gameObject)
    {
        string callerInfo;

        try
        {
            System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
            var method = frame.GetMethod();

            if (method == null)
            {
                callerInfo = "[unknown method]";
            }
            else if (method.DeclaringType == null)
            {
                callerInfo = $"[unknown class].{method.Name}";
            }
            else
            {
                callerInfo = $"{method.DeclaringType.Name}.{method.Name}";
            }
        }
        catch (Exception ex)
        {
            callerInfo = "[error resolving caller]";
        }

        string path = gameObject != null
            ? SceneNavigator.RetrievePath(gameObject)
            : "[null gameObject]";

        return $"{path}/{callerInfo}";
    }

    // Deprecated methods that forward to the new ones for backward compatibility
    [Obsolete("This is a deprecated method. Use FormatHierarchyTree instead.")]
    public static String GetHierarchyDebugString(Transform rootTransform) =>
        FormatHierarchyTree(rootTransform);

    /// <summary>
    /// Retrieves the name of the method that called this method, including the type it belongs to.
    /// Useful for logging and debugging purposes to trace execution flow.
    /// </summary>
    /// <returns>A string containing the name of the caller method and its declaring type in the format "Type.Method".</returns>
    /// <remarks>
    /// This method is deprecated because it doesn't handle null reference exceptions properly.
    /// Use ResolveCallerMethod instead, which includes proper error handling.
    /// </remarks>
    [Obsolete("This is a deprecated method. Use ResolveCallerMethod instead.")]
    public static String GetCallerInfo()
    {
        System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
        var method = frame.GetMethod();
        return $"{method.DeclaringType.Name}.{method.Name}";
    }
}