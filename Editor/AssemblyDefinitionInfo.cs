using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
namespace Unity.PackageManagement
{
    [Serializable]
    public class AssemblyDefinitionInfo
    {
        public string name;
        public string rootNamespace;
        public string[] references;
        public string[] includePlatforms;
        public string[] excludePlatforms;
        public bool allowUnsafeCode;
        public bool overrideReferences;
        public string[] precompiledReferences;
        public bool autoReferenced;
        public string[] defineConstraints;
        public string[] versionDefines;
        public bool noEngineReferences;

        [NonSerialized]
        public string path;

        public static AssemblyDefinitionInfo Load(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            AssemblyDefinitionInfo asmDef = null;
            try
            {
                asmDef = JsonUtility.FromJson<AssemblyDefinitionInfo>(json);
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrEmpty(asmDef.name))
            {
                return null;
            }
            asmDef.path = path;
            return asmDef;
        }

        public static AssemblyDefinitionInfo[] Find(string path)
        {
            List<AssemblyDefinitionInfo> list = new();
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "*.asmdef", SearchOption.AllDirectories))
                {
                    var asmDef = Load(file);
                    if (asmDef == null)
                        continue;
                    list.Add(asmDef);
                }
            }

            return list.ToArray();
        }

    }
}