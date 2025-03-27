#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
public class JsonPackageImporter
{
    private static AddRequest addRequest;

    static JsonPackageImporter()
    {
        CheckJsonNetPackage();
    }

    private static void CheckJsonNetPackage()
    {
        // Check if Newtonsoft.Json is already installed by trying to get the type
        var jsonConvertType = System.Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json");
        if (jsonConvertType != null)
        {
            // Package is already installed
            return;
        }

        // Ask the user if they want to install the package
        bool installPackage = EditorUtility.DisplayDialog(
            "Json.NET Package Required",
            "This project requires the Newtonsoft Json.NET package for proper JSON parsing. Would you like to install it now?",
            "Install",
            "Cancel"
        );

        if (!installPackage)
        {
            Debug.LogWarning("Json.NET package installation was cancelled. Some functionality may not work correctly.");
            return;
        }

        // Install the package
        Debug.Log("Installing Newtonsoft Json.NET package...");
        addRequest = Client.Add("com.unity.nuget.newtonsoft-json");
        EditorApplication.update += CheckAddRequest;
    }

    private static void CheckAddRequest()
    {
        if (addRequest == null || !addRequest.IsCompleted)
            return;

        EditorApplication.update -= CheckAddRequest;

        if (addRequest.Status == StatusCode.Success)
        {
            Debug.Log("Successfully installed Newtonsoft Json.NET package.");
        }
        else if (addRequest.Status >= StatusCode.Failure)
        {
            Debug.LogError($"Failed to install Newtonsoft Json.NET package: {addRequest.Error.message}");
        }

        // Refresh asset database after package changes
        AssetDatabase.Refresh();
    }
}
#endif