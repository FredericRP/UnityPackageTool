using LitJson;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace FredericRP.PackageTool
{
  //[CreateAssetMenu(menuName = "Window/FredericRP/New Dependency Data")]
  public class CopyAssemblyDataToPackage
  {
    [System.Serializable]
    class AssemblyData
    {
      public string name;
      public string rootNamespace;
      public List<string> references;
      public List<string> includePlatforms;
      public List<string> excludePlatforms;
      public bool allowUnsafeCode;
      public bool overrideReferences;
      public List<string> precompiledReferences;
      public bool autoReferenced = true;
      public List<string> defineConstraints;
      public List<VersionDefineData> versionDefines;
      public bool noEngineReferences;
      public List<string> optionalUnityReferences;
    }

    [System.Serializable]
    class VersionDefineData
    {
      public string name;
      public string expression;
      public string define;
    }
    [System.Serializable]
    class PackageData
    {
      public string filename;
      public string name;
      public string version;
      public string displayName;
      public string description;
      public string unity;
      public string documentationUrl;
      public string[] keywords;
      public AuthorData author;
      public List<DependencyData> dependencies;

      public override string ToString()
      {
        string jsonString = "{\n";
        jsonString += AddField("name", name);
        jsonString += AddField("version", version);
        jsonString += AddField("displayName", displayName);
        jsonString += AddField("description", description);
        jsonString += AddField("unity", unity);
        jsonString += AddField("documentationUrl", documentationUrl);
        if (keywords != null && keywords.Length > 0)
        {
          jsonString += "\t\"keywords\": [\n";
          for (int i = 0; i < keywords.Length; i++)
          {
            if (i > 0)
              jsonString += ",\n";
            jsonString += $"\t\t\"{keywords[i]}\"";
          }
          jsonString += "\n\t],\n";
        }
        if (dependencies != null && dependencies.Count > 0)
        {
          jsonString += "\t\"dependencies\": {\n";
          for (int i = 0; i < dependencies.Count; i++)
          {
            if (i > 0)
              jsonString += ",\n";
            jsonString += $"\t\t\"{dependencies[i].name}\": \"{dependencies[i].version}\"";
          }
          jsonString += "\n\t},\n";
        }
        if (author != null)
        {
          jsonString += "\t\"author\": {\n";
          jsonString += "\t" + AddField("name", author.name);
          jsonString += "\t" + AddField("email", author.email);
          jsonString += "\t" + AddField("url", author.url, false);
          jsonString += "\t}\n";
        }
        jsonString += "}";
        // Finally... replace tabulation with SPACES
        return jsonString.Replace("\t", "    ");
      }

      string AddField(string key, string value, bool trailingComma = true)
      {
        if (value == null)
          return "";
        return $"\t\"{key}\": \"{value}\"" + (trailingComma ? "," : "") + "\n";

      }
    }

    [System.Serializable]
    class AuthorData
    {
      public string name;
      public string email;
      public string url;
    }

    [System.Serializable]
    class DependencyData
    {
      public string name;
      public string version;
    }

    static PackageCollection packageCollection;
    static Dictionary<string, string> assemblyNameToPath;

    //[MenuItem("Window/FredericRP/List Project Packages")]
    static void ListProjectPackages()
    {
      assemblyNameToPath = new Dictionary<string, string>();
      ListRequest listRequest = Client.List(true, false);
      while (!listRequest.IsCompleted)
      { }
      if (listRequest.Status == StatusCode.Success)
      {
        packageCollection = listRequest.Result;
        foreach (var package in packageCollection)
        {
          //Debug.Log($"Search in {package.resolvedPath}");
          string[] asmList = Directory.GetFiles(package.resolvedPath, "*.asmdef", SearchOption.AllDirectories);
          for (int i = 0; i < asmList.Length; i++)
          {
            string asmdefText = File.ReadAllText(asmList[i]);
            AssemblyData asmData = JsonUtility.FromJson<AssemblyData>(asmdefText);
            assemblyNameToPath.Add(asmData.name, package.resolvedPath.Substring(package.resolvedPath.LastIndexOf(Path.DirectorySeparatorChar) + 1));
            //Debug.Log("Found assembly " + asmData.name + " at path " + package.resolvedPath.Substring(package.resolvedPath.LastIndexOf(Path.DirectorySeparatorChar) + 1));
          }
        }
      }
      else
      {
        Debug.LogWarning("Could not retrieve the package list " + listRequest.Error);
      }
    }

    [MenuItem("Window/FredericRP/Update dependencies")]
    public static void CopyDependencies()
    {
      // Create a list of assembly definition files from project Packages for future reference
      ListProjectPackages();
      // Update local package.json files found from Assets assembly definition files
      UpdatePackageFile();
    }

    static void UpdatePackageFile()
    {
      List<AssemblyData> assemblyDataList;
      List<PackageData> packageList;
      assemblyDataList = new List<AssemblyData>();
      List<string> packageFileList = new List<string>();
      Dictionary<string, string> assemblyToPackage = new Dictionary<string, string>();
      Dictionary<string, int> packageToDependency = new Dictionary<string, int>();
      string[] asmdefList = AssetDatabase.FindAssets("FredericRP."); // does not support file extension search filter like *.asmdef (and AssemblyDefinition is not a known type... (sic) )
                                                                     // 1. List assembly definition and required "references"
      for (int i = 0; i < asmdefList.Length; i++)
      {
        TextAsset text = (TextAsset)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(asmdefList[i]), typeof(TextAsset));
        AssemblyData asmData = JsonUtility.FromJson<AssemblyData>(text?.text);
        // Manage only Runtime assembly definitions (editor references the same + the Runtime one)
        if (asmData != null && asmData.name.EndsWith(".Runtime"))
        {
          //Debug.Log("Loaded asm [" + AssetDatabase.GUIDToAssetPath(asmdefList[i]) + "] load " + asmData.name);
          // Extrapolate package filename
          // Folder structure: 
          // PluginName
          // |-package.json
          // |-Editor
          //  |- CompanyName.PluginName.Editor
          // |-Runtime
          //  |- CompanyName.PluginName.Runtime
          DirectoryInfo directory = new DirectoryInfo(AssetDatabase.GUIDToAssetPath(asmdefList[i])).Parent.Parent;
          // Substract project assets root path as it's not allowed on LoadAssetAtPath method
          string packagePath = "Assets" + directory.FullName.Substring(Application.dataPath.Length).Replace("\\", "/") + "/package.json";
          if (!packageFileList.Contains(packagePath))
          {
            packageFileList.Add(packagePath);
          }
          assemblyDataList.Add(asmData);
          // Add a link between assembly definition name and json package
          assemblyToPackage.Add(asmData.name, packagePath);
          packageToDependency.Add(packagePath, assemblyDataList.Count - 1);
        }
      }
      //Debug.LogWarning("Ended with " + asmdefList.Length + " assemblies");
      // 2. Read packages.json to match assembly definition and version
      packageList = new List<PackageData>();
      for (int i = 0; i < packageFileList.Count; i++)
      {
        LoadPackage(ref packageList, packageFileList[i]);
      }
      //Debug.LogWarning("Ended with " + packageFileList.Count + " packages");
      int found = 0;
      int search = 0;
      // 3. Update packages.json to reflect existing dependencies with package version
      for (int i = 0; i < packageList.Count; i++)
      {
        string dependantPackageFilename = packageList[i].filename;
        // - for each package.json, take the corresponding  Assembly Definition, transpose their references to other packages, either from project inner package.json files or project unity packages
        AssemblyData asmData = assemblyDataList[packageToDependency[dependantPackageFilename]];
        for (int j = 0; j < asmData.references.Count; j++)
        {
          //Debug.Log("Search for " + asmData.references[j]);
          search++;
          // For each known reference (ex. of unknown reference that should not be included here: TextMeshPro)
          if (assemblyToPackage.ContainsKey(asmData.references[j]))
          {
            string packageFilename = assemblyToPackage[asmData.references[j]];
            // Take the corresponding package.json to get the version
            PackageData packageData = packageList.Find(package => package.filename.Equals(packageFilename));
            DependencyData dependencyData = null;
            if (packageData != null)
            {
              //Debug.LogWarning("Found " + packageData.name + " " + packageData.version);
              dependencyData = new DependencyData();
              dependencyData.name = packageData.name;
              dependencyData.version = packageData.version;
              found++;
            }
            // And update it back in the dependent package.json file
            if (dependencyData != null)
            {
              PackageData dependentPackageData = packageList.Find(package => package.filename.Equals(dependantPackageFilename));
              dependentPackageData.dependencies.Add(dependencyData);
            }
          }
          else
          {
            // References is unknown, search in unity packages assembly definition list the matching name
            // the dictionary match the assembly display name to the package SemVer directly
            if (assemblyNameToPath.ContainsKey(asmData.references[j]))
            {
              // package def format: <com.package.name>@<version> where <version> is like 1.0.0
              string[] packageDef = assemblyNameToPath[asmData.references[j]].Split('@');
              string packageName = packageDef[0];
              string packageVersion = (packageDef.Length > 0) ? packageDef[1] : "";
              DependencyData dependencyData = null;
              //Debug.LogWarning("Found in Unity packages: " + packageName + " v" + packageVersion);
              dependencyData = new DependencyData();
              dependencyData.name = packageName;
              dependencyData.version = packageVersion;
              found++;
              // And update it back in the dependent package.json file
              PackageData dependentPackageData = packageList.Find(package => package.filename.Equals(dependantPackageFilename));
              dependentPackageData.dependencies.Add(dependencyData);
            }
          }
        }
        //Debug.Log("Write to [" + dependantPackageFilename + "]: " + packageList[i].ToString());
        // Write back to the package file
        //*
        StreamWriter writer = new StreamWriter(dependantPackageFilename, false);
        writer.WriteLine(packageList[i].ToString());
        writer.Close();
        //Re-import the file to update the reference in the editor
        AssetDatabase.ImportAsset(dependantPackageFilename);
        // */
      }
      if (found < search)
        Debug.LogWarning($"Found {found} packages for {search} searched packages.");
    }

    static void LoadPackage(ref List<PackageData> packageList, string packageFilename)
    {
      TextAsset text = (TextAsset)AssetDatabase.LoadAssetAtPath(packageFilename, typeof(TextAsset));
      //Debug.Log("Load package.json [" + packageFileList[i] + "/package.json" + "]");
      try
      {
        PackageData packageData = JsonUtility.FromJson<PackageData>(text?.text);
        // Dependencies can not be loaded with JsonUtility, we load them additionnaly with LitJson but keep the other data to be able to write back the full updated data
        JsonData jsonPackageData = JsonMapper.ToObject(text.text);
        packageData.filename = packageFilename;
        if (jsonPackageData != null && packageData != null)
        {
          //Debug.Log("Loaded " + packageData.name);
          // Create dependencies but do not fill it as it will be updated right after
          packageData.dependencies = new List<DependencyData>();
          packageList.Add(packageData);
        }
      }
      catch (System.Exception e)
      {
        Debug.LogError("Error while loading " + packageFilename + ": " + e);
      }
    }
  }
}