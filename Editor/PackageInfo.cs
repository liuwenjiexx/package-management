using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Unity.PackageManagement
{

    [Serializable]
    public class PackageInfo
    {
        public string name;
        public string displayName;
        public string version;
        public string unity;
        public string[] keywords;
        public string category;
        public string description;
        public PackageDependency[] dependencies;
        [HideInInspector]
        public string path;

        [NonSerialized]
        public string location;

        private Version _version;
        public Version Version
        {
            get
            {
                if (_version == null)
                {
                    if (version != null && Version.TryParse(version, out var ver))
                    {
                        _version = ver;
                    }
                    if (_version == null)
                    {
                        _version = Version.Empty;
                    }
                }
                return _version;
            }
            set
            {
                _version = value;
                version = _version.ToString();
            }
        }

        public PackageFlags flags;

        public bool IsFavorite
        {
            get
            {
                //if ((flags & PackageFlags.Favorite) != 0)
                //    return true;
                //if (owner != null)
                //{
                //    return owner.IsFavorite(this);
                //}
                //return false;
                return (flags & PackageFlags.Favorite) != 0;
            }
        }

        public bool IsUsed => (flags & PackageFlags.Used) == PackageFlags.Used;

        public int totalCodeFile;
        public int totalCodeLine;

        [NonSerialized]
        public PackageRepository owner;

        public const string FILE_NAME = "package.json";
        public class Properties
        {
            public const string Version = "version";
            public const string Name = "name";
            public const string DisplayName = "displayName";
            public const string Unity = "unity";
            public const string HideInEditor = "hideInEditor";
            public const string Samples = "samples";
            public const string Author = "author";
            public const string AuthorName = "name";
            public const string Dependencies = "dependencies";

        }
        public string FilePath
        {
            get
            {
                string dir = FullDir;
                if (!string.IsNullOrEmpty(dir))
                {
                    return Path.Combine(dir, FILE_NAME);
                }
                return null;
            }
        }

        public string FullDir
        {
            get
            {
                if (!string.IsNullOrEmpty(owner?.localDir))
                {
                    return Path.GetFullPath(Path.Combine(owner.localDir, path));
                }
                if (!string.IsNullOrEmpty(location))
                    return location;
                return null;
            }
        }

        public string GetManifestUri()
        {
            string dir = FullDir;
            if (!string.IsNullOrEmpty(dir))
            {
                string _path = Path.GetFullPath(dir);
                _path = EditorPackageUtility.ToRelativePath(_path, "Packages");
                _path = EditorPackageUtility.NormalPath(_path);
                _path = $"file:{_path}";
                return _path;
            }
            return version;
        }

        public void OpenInspector()
        {
            var inspectorObject = ScriptableObject.CreateInstance<InspectorObject>();
            inspectorObject.target = this;
            inspectorObject.hideFlags = HideFlags.DontSave;
            Selection.activeObject = inspectorObject;
        }

        internal class InspectorObject : ScriptableObject
        {
            public PackageInfo target;


            private void OnDisable()
            {
                if (Selection.activeObject == this)
                    Selection.activeObject = null;
            }

        }

        public static void TryParse(string path, PackageInfo packageInfo)
        {
            _TryParse(path, packageInfo);
        }

        public static PackageInfo TryParse(string path)
        {
            return _TryParse(path, null);
        }

        static PackageInfo _TryParse(string path, PackageInfo packageInfo)
        {
            try
            {
                if (packageInfo != null)
                {
                    packageInfo.flags &= ~PackageFlags.Missing;
                }

                string file = Path.Combine(path, FILE_NAME);
                if (!File.Exists(file))
                {
                    if (packageInfo != null)
                    {
                        packageInfo.flags |= PackageFlags.Missing;
                    }
                    return null;
                }

                string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                if (string.IsNullOrEmpty(json))
                {
                    if (packageInfo != null)
                    {
                        packageInfo.flags |= PackageFlags.Missing;
                    }
                    return null;
                }

                if (packageInfo == null)
                {
                    packageInfo = JsonUtility.FromJson<PackageInfo>(json);
                    packageInfo.location = path;
                }
                else
                {
                    JsonUtility.FromJsonOverwrite(json, packageInfo);
                    packageInfo._version = null;
                }


                if (packageInfo == null)
                    return null;

                if (string.IsNullOrEmpty(packageInfo.name))
                {
                    packageInfo.flags |= PackageFlags.Missing;
                    return null;
                }

            }
            catch
            {
                if (packageInfo != null)
                {
                    packageInfo.flags |= PackageFlags.Missing;
                }
            }


            return packageInfo;
        }


        static Regex PropertyRegex = new Regex($"^(?<start_space>\\s*)\"(?<key>\\S+)\"\\s*:\\s*(\"(?<string_value>\\S+)\"|(?<value>\\S+))", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        static object ParseValue(string str)
        {
            object value = null;
            if (string.IsNullOrEmpty(str))
            {
                value = null;
            }
            else if (str == "true")
            {
                value = true;
            }
            else if (str == "false")
            {
                value = false;
            }
            else if (str == "null")
            {
                value = null;
            }
            else if (float.TryParse(str, out var f))
            {
                value = f;
            }
            return value;
        }

        static string ValueToString(object value)
        {
            if (value == null) return "null";
            if (value is string str)
            {
                return $"\"{str}\"";
            }
            else if (value is bool b)
            {
                return b ? "true" : "false";
            }
            return value.ToString();
        }

        public static Dictionary<string, object> ParseProperties(string packageJson)
        {
            Dictionary<string, object> values = new();
            foreach (Match m in PropertyRegex.Matches(packageJson))
            {
                string key = m.Groups["key"].Value;
                object value = null;
                if (m.Groups["string_value"].Success)
                {
                    value = m.Groups["string_value"].Value;
                }
                else
                {
                    var str = m.Groups["value"].Value;
                    value = ParseValue(str);
                }
                values[key] = value;

            }
            return values;
        }

        public static string ReplaceProperties(string packageJson, Dictionary<string, object> values)
        {
            string newText = PropertyRegex.Replace(packageJson, m =>
            {
                string result = m.Value;
                string key = m.Groups["key"].Value;

                string oldStrValue;

                if (m.Groups["string_value"].Success)
                {
                    var valueGroup = m.Groups["string_value"];
                    oldStrValue = valueGroup.Value;
                }
                else
                {
                    oldStrValue = m.Groups["value"].Value;
                }

                if (values.TryGetValue(key, out var value))
                {
                    string strValue = ValueToString(value);
                    if (strValue != oldStrValue)
                    {
                        var m2 = PropertyRegex.Match(m.Value);
                        Group valueGroup;
                        if (m2.Groups["string_value"].Success)
                        {
                            valueGroup = m2.Groups["string_value"];
                        }
                        else
                        {
                            valueGroup = m2.Groups["value"];
                        }

                        result = result.Substring(0, valueGroup.Index) + value + result.Substring(valueGroup.Index + valueGroup.Length);

                        //if (value == null || value is string)
                        //{
                        //    return $"{m.Groups["start_space"].Value}\"{key}\": \"{strValue}\"";
                        //}
                        //else
                        //{
                        //    return $"{m.Groups["start_space"].Value}\"{key}\": {strValue}";
                        //}
                    }
                }
                return result;
            });
            return newText;
        }

        static Encoding Encoding = new UTF8Encoding(false);

        public static Dictionary<string, object> ReadProperties(string packagePath)
        {
            if (!File.Exists(packagePath))
                return new();
            string text = File.ReadAllText(packagePath, Encoding.UTF8);
            return ParseProperties(text);
        }

        public static bool WriteProperty(string packagePath, string key, string value)
        {
            return WriteProperties(packagePath, new() { { key, value } });
        }

        public static bool WriteProperties(string packagePath, Dictionary<string, object> values)
        {
            string text = File.ReadAllText(packagePath, Encoding.UTF8);
            string newText = ReplaceProperties(text, values);
            if (newText != text)
            {
                File.WriteAllText(packagePath, newText, Encoding);
                return true;
            }
            //foreach (Match m in regex.Matches(text))
            //{
            //    if (m.Groups["key"].Value == key)
            //    {
            //        var valueGroup = m.Groups["value"];
            //        string oldValue = valueGroup.Value;
            //        if (oldValue != value)
            //        {
            //            StringBuilder builder = new StringBuilder();
            //            builder.Append(text.Substring(0, valueGroup.Index));
            //            builder.Append(value);
            //            builder.Append(text.Substring(valueGroup.Index + valueGroup.Length));
            //            File.WriteAllText(packagePath, builder.ToString(), Encoding.UTF8);
            //            return true;
            //        }
            //        return false;
            //    }

            //}
            return false;
        }
        public override string ToString()
        {
            return $"{name} ({version})";
        }

    }

    [Serializable]
    public class PackageDependency
    {
        public string name;
        public string version;
        [HideInInspector]
        public Version Version;
        [HideInInspector]
        public string localPath;

        public void Update()
        {
            Version = null;
            localPath = null;

            //if (!System.Version.TryParse(version, out Version))
            //{
            //    if (version.StartsWith("file:"))
            //    {
            //        localPath = version.Substring("file:".Length);
            //    }
            //}
            if (!Version.TryParse(version, out Version))
            {
                if (version.StartsWith("file:"))
                {
                    localPath = version.Substring("file:".Length);
                }
            }
        }



        public override string ToString()
        {
            return $"{name}({version})";
        }

    }


    public enum PackageFlags
    {
        None = 0,
        PackageCache = 1 << 0,
        Local = 1 << 10,
        Missing = 1 << 11,
        Favorite = 1 << 12,

        Used = 1 << 15,
        ProjectUsed = Used | 1 << 16,
        ManifestUsed = Used | 1 << 17,
        VersionUsed = Used | 1 << 18,
        LocalUsed = Used | 1 << 19,
        LinkUsed = Used | 1 << 20,
        Saved = PackageCache,
    }

}