using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Git;
using Unity.PackageManagement;
using static UnityEditor.EditorApplication;
using Debug = UnityEngine.Debug;


namespace Unity.PackageManagement
{
    internal static class EditorPackageUtility
    {
        internal const string DEFINE_DEBUG = "PACKAGE_MANAGEMENT_DEBUG";

        public static string UnityPackageName => GetPackageName(typeof(EditorPackageUtility).Assembly);

        public const string PackageManifestPath = "Packages/manifest.json";

        public const string PackagePackageFile = "package.json";
        public const string PackageRuntimeFolder = "Runtime";

        public const string PackageEditorFolder = "Editor";

        public const string PackageTestsFolder = "Tests";

        public const string PackageDocumentationFolder = "Documentation~";

        public const string PackageSamplesFolder = "Samples~";

        public const string PackageReadmeFile = "README.md";
        public const string PackageChangelogFile = "CHANGELOG.md";
        public const string PackageLicenseFile = "LICENSE.md";


        private static PackageRepository starRepsitory;

        private static PackageRepository projectRepsitory;

        static EditorPackageUtility()
        {




        }

        public static PackageRepository StarRepsitory
        {
            get
            {
                if (starRepsitory == null)
                {
                    starRepsitory = new PackageRepository()
                    {
                        editable = false,
                        name = "★"
                    };
                }
                return starRepsitory;
            }
        }

        public static PackageRepository ProjectRepsitory
        {
            get
            {
                if (projectRepsitory == null)
                {
                    string repoPath = Path.Combine(PackageRepository.GetLocalRepositoryDir(Environment.CurrentDirectory), "Repository.json");
                    bool exists = File.Exists(repoPath);

                    projectRepsitory = PackageRepository.LoadLocal(Environment.CurrentDirectory);
                    projectRepsitory.name = "[Project]";
                    projectRepsitory.Update();
                    if (!exists)
                    {
                        projectRepsitory.ScanLocalPackages();

                        foreach (var pkg in projectRepsitory.GetPackages())
                        {
                            UpdatePackage(pkg);
                        }
                        projectRepsitory.Save();
                    }
                }
                return projectRepsitory;
            }
        }


        internal static string GetUSSPath(string uss, Type type = null)
        {
            string dir = (type ?? typeof(EditorPackageUtility)).Assembly.GetPackageDirectory();
            if (string.IsNullOrEmpty(dir))
                return null;
            return $"{dir}/Editor/USS/{uss}.uss";
        }

        internal static string GetUXMLPath(string uxml, Type type = null)
        {
            string dir = (type ?? typeof(EditorPackageUtility)).Assembly.GetPackageDirectory();
            return $"{dir}/Editor/UXML/{uxml}.uxml";
        }
        public static StyleSheet TryAddStyle(this VisualElement elem, Type type, string uss = null)
        {

            if (uss == null)
            {
                uss = type.Name;
            }
            string assetPath = GetUSSPath(uss, type);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);
            if (style)
            {
                elem.styleSheets.Add(style);
            }
            return style;
        }

        public static StyleSheet AddStyle(this VisualElement elem, Type type, string uss = null)
        {
            return TryAddStyle(elem, type, uss);
        }

        public static StyleSheet AddStyle(this VisualElement elem, string uss)
        {
            return AddStyle(elem, typeof(EditorPackageUtility), uss);
        }

        internal static TemplateContainer LoadUXML(this Type type, string uxml, VisualElement parent = null)
        {
            if (string.IsNullOrEmpty(uxml))
                uxml = type.Name;

            string path = GetUXMLPath(uxml, type);
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            TemplateContainer treeRoot = null;
            if (asset)
            {
                treeRoot = asset.CloneTree();
                string uss = null;


                if (!string.IsNullOrEmpty(uss))
                    treeRoot.AddStyle(type, uss);
                else
                    treeRoot.TryAddStyle(type, uxml);

                if (parent != null)
                {
                    parent.Add(treeRoot);
                }
            }
            return treeRoot;
        }


        #region Assembly Metadata

        static Dictionary<Assembly, Dictionary<string, string[]>> allAssemblyMetadatas;

        public static string GetAssemblyMetadata(this Assembly assembly, string key)
        {
            if (!TryGetAssemblyMetadata(assembly, key, out var value))
                throw new Exception($"Not found AssemblyMetadataAttribute. key: {key}");
            return value;
        }

        public static string GetAssemblyMetadata(this Assembly assembly, string key, string defaultValue)
        {
            if (!TryGetAssemblyMetadata(assembly, key, out var value))
            {
                value = defaultValue;
            }
            return value;
        }


        public static Dictionary<string, string[]> GetAssemblyMetadatas(this Assembly assembly)
        {
            if (allAssemblyMetadatas == null)
                allAssemblyMetadatas = new();

            Dictionary<string, string[]> metadatas;
            if (!allAssemblyMetadatas.TryGetValue(assembly, out metadatas))
            {
                var tmp = new Dictionary<string, List<string>>();

                foreach (var attr in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    if (!tmp.TryGetValue(attr.Key, out var list))
                    {
                        list = new();
                        tmp[attr.Key] = list;
                    }
                    list.Add(attr.Value);
                }
                metadatas = tmp.ToDictionary(k => k.Key, v => v.Value.ToArray());
                allAssemblyMetadatas[assembly] = metadatas;
            }
            return metadatas;
        }

        public static bool TryGetAssemblyMetadata(this Assembly assembly, string key, out string value)
        {
            var metadatas = GetAssemblyMetadatas(assembly);
            if (metadatas.TryGetValue(key, out var list))
            {
                value = list[0];
                return true;
            }
            value = null;
            return false;
        }

        #endregion

        #region UnityPackage

        static Dictionary<string, string> unityPackageDirectories = new Dictionary<string, string>();


        public static string GetPackageName(this Assembly assembly)
        {
            string packageName;
            if (!TryGetAssemblyMetadata(assembly, "Package.Name", out packageName))
            {
                if (!TryGetAssemblyMetadata(assembly, "Unity.Package.Name", out packageName))
                {
                    return null;
                }
            }


            return packageName;
        }

        public static string GetPackageDirectory(this Assembly assembly)
        {
            return GetUnityPackageDirectory(GetPackageName(assembly));
        }

        //2021/4/13
        internal static string GetUnityPackageDirectory(string packageName)
        {
            if (!unityPackageDirectories.TryGetValue(packageName, out var path))
            {
                var tmp = Path.Combine("Packages", packageName);
                if (Directory.Exists(tmp) && File.Exists(Path.Combine(tmp, "package.json")))
                {
                    path = tmp;
                }

                if (path == null)
                {
                    foreach (var dir in Directory.GetDirectories("Assets", "*", SearchOption.AllDirectories))
                    {
                        if (string.Equals(Path.GetFileName(dir), packageName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (File.Exists(Path.Combine(dir, "package.json")))
                            {
                                path = dir;
                                break;
                            }
                        }
                    }
                }

                if (path == null)
                {
                    foreach (var pkgPath in Directory.GetFiles("Assets", "package.json", SearchOption.AllDirectories))
                    {
                        try
                        {
                            if (JsonUtility.FromJson<_UnityPackage>(File.ReadAllText(pkgPath, System.Text.Encoding.UTF8)).name == packageName)
                            {
                                path = Path.GetDirectoryName(pkgPath);
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (path != null)
                {
                    path = path.Replace('\\', '/');
                }
                unityPackageDirectories[packageName] = path;
            }
            return path;
        }

        [Serializable]
        class _UnityPackage
        {
            public string name;
        }

        #endregion


        /// <summary>
        /// 获取相对路径
        /// </summary>
        internal static bool ToRelativePath(this string path, string relativeTo, out string result)
        {
            string fullRelativeTo = Path.GetFullPath(relativeTo).Trim();
            string fullPath = Path.GetFullPath(path).Trim();

            fullRelativeTo = NormalPath(fullRelativeTo);
            fullPath = NormalPath(fullPath);

            if (fullPath.EndsWith("/"))
                fullPath = fullPath.Substring(0, fullPath.Length - 1);
            if (fullRelativeTo.EndsWith("/"))
                fullRelativeTo = fullRelativeTo.Substring(0, fullRelativeTo.Length - 1);

            string[] relativeToParts = fullRelativeTo.Split('/');
            string[] fullPathParts = fullPath.Split('/');
            int index = -1;

            if (fullPathParts.Length <= 1)
            {
                result = path;
                return false;
            }

            if (!string.Equals(fullPathParts[0], relativeToParts[0], StringComparison.InvariantCultureIgnoreCase))
            {
                result = path;
                return false;
            }


            for (int i = 0; i < fullPathParts.Length && i < relativeToParts.Length; i++)
            {
                if (!string.Equals(fullPathParts[i], relativeToParts[i], StringComparison.InvariantCultureIgnoreCase))
                    break;
                index = i;
            }

            result = "";
            for (int i = index + 1; i < relativeToParts.Length; i++)
            {
                if (result.Length > 0)
                    result += '/';
                result += "..";
            }
            for (int i = index + 1; i < fullPathParts.Length; i++)
            {
                if (result.Length > 0)
                    result += '/';
                result += fullPathParts[i];
            }
            return true;
        }
        internal static string ToRelativePath(this string path, string relativeTo)
        {
            string result;
            if (ToRelativePath(path, relativeTo, out result))
                return result;
            return path;
        }

        public static void UpdatePackage(PackageInfo package)
        {
            string dir = Path.GetDirectoryName(package.FilePath);
            package.totalCodeFile = 0;
            package.totalCodeLine = 0;
            if (Directory.Exists(dir))
            {
                //Debug.Log(package.name + ", " + package.flags);
                if (((package.flags & PackageFlags.PackageCache) != PackageFlags.PackageCache))
                {
                    int totalCodeLine = 0;
                    foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                    {
                        package.totalCodeFile++;
                        totalCodeLine += GetCodeLineCount(file);
                    }
                    package.totalCodeLine = totalCodeLine;
                }
            }


        }

        static int GetCodeLineCount(string path)
        {
            if (!File.Exists(path))
                return 0;
            int count = 0;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                count++;
            }
            return count;
        }


        //[MenuItem("Test/CodeLines")]
        static void CodeLines()
        {
            List<CodeInfo> infos = new List<CodeInfo>();
            string[] rootPaths = new string[] { "Assets", "Packages" };
            foreach (var dir in rootPaths)
            {
                foreach (var file in Directory.GetFiles(dir, "*.asmdef", SearchOption.AllDirectories))
                {
                    string assemblyName = Path.GetFileNameWithoutExtension(file);
                    string directory = Path.GetDirectoryName(file);
                    CodeInfo info = new CodeInfo();
                    info.assemblyName = assemblyName;
                    info.directory = NormalPath(directory);
                    infos.Add(info);
                }
            }

            infos.Sort((a, b) =>
            {
                return -(a.directory.Split('/').Length - b.directory.Split('/').Length);
            });

            foreach (var dir in rootPaths)
            {
                if (infos.FindIndex(o => o.directory == dir) < 0)
                {
                    infos.Add(new CodeInfo()
                    {
                        assemblyName = dir + "-Default",
                        directory = dir
                    });
                }
            }


            foreach (var dir in rootPaths)
            {
                foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string file2 = NormalPath(file);
                    CodeInfo codeInfo = null;
                    foreach (var info in infos)
                    {
                        if (file2.StartsWith(info.directory + "/"))
                        {
                            codeInfo = info;
                            break;
                        }
                    }

                    codeInfo.totalCodeLine += GetCodeLineCount(file);
                }
            }

            foreach (var info in infos.OrderByDescending(o => o.totalCodeLine))
            {
                Debug.Log($"{info.assemblyName}, {info.totalCodeLine:#,###}");
            }
        }

        class CodeInfo
        {
            public string assemblyName;
            public string directory;
            public int totalCodeLine;
        }

        internal static string NormalPath(string path)
        {
            if (path == null)
                return null;
            path = path.Replace('\\', '/');
            if (path.EndsWith("/"))
                path = path.Substring(0, path.Length - 1);
            return path;
        }

        public static string NormalPathLocal(string path)
        {
            if (path == null)
                return null;
            if (Path.DirectorySeparatorChar == '/')
            {
                path = path.Replace('\\', Path.DirectorySeparatorChar);
            }
            else
            {
                path = path.Replace('/', Path.DirectorySeparatorChar);
            }
            if (path.EndsWith(Path.DirectorySeparatorChar))
                path = path.Substring(0, path.Length - 1);
            return path;
        }



        internal static bool IsSymbolicLink(string path)
        {
            if (Directory.Exists(path))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(path);
                if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return true;
            }
            else
            {
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Exists && fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return true;
            }

            return false;
        }

        internal static bool IsSymbolicLink(string path, bool recursion = false)
        {
            if (!(Directory.Exists(path) || File.Exists(path)))
            {
                return false;
            }

            if (IsSymbolicLink(path))
                return true;

            if (recursion)
            {
                string parent;
                parent = Path.GetDirectoryName(path);
                DirectoryInfo dirInfo;
                while (parent != null)
                {
                    dirInfo = new DirectoryInfo(parent);
                    if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        return true;

                    parent = Path.GetDirectoryName(parent);
                }
            }
            return false;
        }


        private static List<PackageDependency> dependencies;
        public static List<PackageDependency> GetManifestDependencies()
        {
            if (dependencies == null)
            {
                dependencies = GetPackageDependencies(PackageManifestPath);
            }
            return dependencies;
        }


        public static List<PackageDependency> GetPackageDependencies(string packagePath)
        {

            var dependencies = new List<PackageDependency>();

            string json = File.ReadAllText(packagePath, Encoding.UTF8);
            var m = Regex.Match(json, "\"dependencies\"\\s*:");
            if (m.Success)
            {
                int startIndex = json.IndexOf('{', m.Index) + 1;
                int endIndex = json.IndexOf('}', startIndex) - 1;
                string text = json.Substring(startIndex, endIndex - startIndex + 1);

                foreach (Match m2 in Regex.Matches(text, "\"(?<name>.+)\"\\s*:\\s*\"(?<uri>.+)\""))
                {
                    dependencies.Add(new PackageDependency()
                    {
                        name = m2.Groups["name"].Value,
                        version = m2.Groups["uri"].Value
                    });
                }
            }
            return dependencies;
        }

        public static void DiryManifest()
        {
            dependencies = null;
        }

        public static bool HasManifestPackage(string name)
        {
            return GetManifestDependencies().Any(o => o.name == name);
        }

        public static bool IsLocalPackageVersion(string version)
        {
            if (version == null)
                return false;
            int index = version.IndexOf("file:");
            return index >= 0;
        }

        private static string PackageVersionToAbsPath(string a)
        {
            if (string.IsNullOrEmpty(a)) return null;

            a = a.Trim();
            int index = a.IndexOf("file:");
            if (index >= 0)
            {
                a = a.Substring("file:".Length);
                a = Path.GetFullPath(a);
                a = NormalPath(a);
                if (a.EndsWith("/"))
                    a = a.Substring(0, a.Length - 1);
            }

            return a;
        }

        private static bool PackageVersionEqual(string a, string b)
        {
            a = PackageVersionToAbsPath(a);
            b = PackageVersionToAbsPath(b);
            return a == b;
        }

        public static bool HasManifestPackage(string name, string version)
        {
            var item = GetManifestDependencies().FirstOrDefault(o => o.name == name);
            if (item == null)
                return false;

            if (PackageVersionEqual(item.version, version))
                return true;

            //if (uri.StartsWith("file:") && item.uri.StartsWith("file:"))
            //{
            //    string path1 = Path.GetFullPath(uri.Substring("file:".Length));
            //    string path2 = Path.GetFullPath(item.uri.Substring("file:".Length));
            //    return string.Equals(path1, path2, StringComparison.InvariantCultureIgnoreCase);
            //}
            return false;
        }

        public static string GetManifestVersion(string name)
        {
            var item = GetManifestDependencies().FirstOrDefault(o => o.name == name);
            if (item == null)
                return null;
            return item.version;
        }

        public static void AddManifestPackage(string name)
        {
            var pkg = GetStarPackageInfo(name);
            if (pkg == null)
            {
                throw new Exception($"Not found package '{name}'");
            }
            AddManifestPackage(name, pkg.version);
        }

        public static bool AddManifestPackage(string name, string version, bool save = true)
        {
            var manifest = GetManifestDependencies();

            bool changed = false;

            if (!HasManifestPackage(name, version))
            {
                changed |= RemoveManifestPackage(manifest, name, false);
                Log($"Manifest Add Package '{name}', version: '{version}'");

                manifest.Add(new PackageDependency()
                {
                    name = name,
                    version = version
                });
                changed = true;
            }

            if (changed && save)
            {
                manifest.Sort((a, b) => StringComparer.Ordinal.Compare(a.name, b.name));
                SaveManifest(manifest);
            }

            return changed;
        }

        public static void AddManifestLocalPackage(string name)
        {
            var pkg = GetStarPackageInfo(name);
            if (pkg == null)
            {
                throw new Exception($"Not found package '{name}'");
            }

            if (string.IsNullOrEmpty(pkg.GetManifestUri()))
            {

                AddManifestLocalPackage(name, pkg.version);
            }
            else
            {
                AddManifestLocalPackage(name, pkg.GetManifestUri());
            }

        }


        public static bool AddManifestLocalPackage(string name, string version, bool save = true)
        {
            var manifest = GetManifestDependencies();

            bool changed = false;

            if (!HasManifestPackage(name, version))
            {
                changed |= RemoveManifestPackage(manifest, name, false);
                Log($"Manifest Add Local Package '{name}', version: '{version}'");

                manifest.Add(new PackageDependency()
                {
                    name = name,
                    version = version
                });
                changed = true;
            }

            if (changed)
            {
                //处理依赖
                foreach (var dep in GetAllDependencies(name))
                {
                    if (dep.name == name)
                        continue;

                    if (HasManifestPackage(dep.name))
                        continue;
                    var pakcage = GetStarPackageInfo(dep.name);

                    if (pakcage == null)
                        continue;

                    string ver = pakcage.GetManifestUri();
                    if (string.IsNullOrEmpty(ver))
                        continue;

                    if (!IsLocalPackageVersion(ver))
                        continue;

                    Log($"Manifest Add Local Package '{dep.name}', version: '{ver}'");

                    manifest.Add(new PackageDependency()
                    {
                        name = dep.name,
                        version = ver
                    });
                    changed = true;
                }
            }

            if (changed && save)
            {
                manifest.Sort((a, b) => StringComparer.Ordinal.Compare(a.name, b.name));
                SaveManifest(manifest);
            }

            return changed;
        }


        public static bool RemoveManifestPackage(PackageInfo packageInfo)
        {
            bool changed = false;

            if (RemoveManifestPackage(packageInfo.name))
            {
                changed = true;
            }

            if (IsLinkPackage(packageInfo))
            {
                string path = DeleteLinkPackage(packageInfo);
                if (path != null)
                {
                    changed = true;
                }
            }

            return changed;
        }

        public static bool RemoveManifestPackage(string name)
        {
            return RemoveManifestPackage(GetManifestDependencies(), name);
        }

        public static bool RemoveManifestPackage(List<PackageDependency> manifest, string name, bool save = true)
        {
            var index = manifest.FindIndex(o => o.name == name);
            if (index < 0)
                return false;
            var pkg = manifest[index];
            manifest.RemoveAt(index);
            Log($"Manifest Remove Package: '{pkg.name}', version: '{pkg.version}'");
            if (save)
            {
                SaveManifest(manifest);
            }
            return true;
        }

        private static void SaveManifest()
        {
            SaveManifest(GetManifestDependencies());
        }

        private static void SaveManifest(List<PackageDependency> manifest)
        {
            StringBuilder builder = new StringBuilder();

            string text = File.ReadAllText(PackageManifestPath, Encoding.UTF8);
            var m = Regex.Match(text, "\"dependencies\"\\s*:");
            int startIndex = text.IndexOf('{', m.Index) + 1;
            int endIndex = text.IndexOf('}', startIndex);
            builder.Append(text.Substring(0, startIndex));

            bool first = true;
            foreach (var package in manifest)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.Append(",");
                }
                builder.Append($"\n    \"{package.name}\": \"{package.version}\"");
            }

            builder.Append("\n  ").Append(text.Substring(endIndex));


            string newText = builder.ToString();
            if (text != newText)
            {
                DiryManifest();
                File.WriteAllText(PackageManifestPath, newText, new UTF8Encoding(false));
            }
        }



        public static PackageInfo GetPackageInfo(string name)
        {
            foreach (var pkg in EditorPackageSettings.LoadRepositories()
                .SelectMany(o => o.GetPackages())
                .OrderByDescending(o => o.IsFavorite))
            {
                if (pkg.name == name)
                {
                    return pkg;
                }
            }
            return null;
        }

        public static PackageInfo GetStarPackageInfo(string name)
        {
            foreach (var pkg in EditorPackageSettings.LoadRepositories()
                .SelectMany(o => o.GetPackages())
                .Where(o => o.IsFavorite))
            {
                if (pkg.name == name)
                {
                    return pkg;
                }
            }
            return null;
        }

        public static PackageDependency[] GetDirectDependencies(string name)
        {
            var package = GetPackageInfo(name);

            if (package == null)
                return new PackageDependency[0];

            return package.dependencies.ToArray();
        }


        public static PackageDependency[] GetAllDependencies(string name)
        {
            var package = GetPackageInfo(name);

            if (package == null)
                return new PackageDependency[0];
            List<PackageDependency> dependencies = new List<PackageDependency>();
            GetAllDependencies(new PackageDependency()
            {
                name = name,
                version = package.GetManifestUri()
            }, dependencies);
            return dependencies.ToArray();
        }


        private static void GetAllDependencies(PackageDependency package, List<PackageDependency> result)
        {
            if (result.Any(o => o.name == package.name))
                return;
            result.Add(package);

            var pkgInfo = GetPackageInfo(package.name);

            if (pkgInfo == null)
            {
                return;
            }

            foreach (var dep in pkgInfo.dependencies)
            {
                GetAllDependencies(dep, result);
            }
        }

        public static string LengthToUnitString(long length)
        {
            if (length < 1024 * 0.5f)
            {
                return $"{length}";
            }
            else if (length < 1024 * 1024 * 0.5)
            {
                return $"{length / 1024d:0.#}K";
            }
            else if (length < 1024 * 1024 * 1024 * 0.5)
            {
                return $"{length / (1024d * 1024):0.#}M";
            }
            else if (length < 1024d * 1024 * 1024 * 1024 * 0.5)
            {
                return $"{length / (1024d * 1024 * 1024):0.#}G";
            }
            return length.ToString();
        }

        internal const string LogPrefix = "[Package] ";

        [Conditional(DEFINE_DEBUG)]
        public static void DebugLog(string message)
        {
            Debug.Log($"{LogPrefix}{message}");
        }


        public static void Log(string message)
        {
            Debug.Log($"{LogPrefix}{message}");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="args"></param>
        /// <param name="workDir"></param>
        /// <param name="onInput">o.WriteLine("Command & exit")</param>
        /// <param name="timeoutMS"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string RunWithResult(string file, IEnumerable<string> args = null, string workDir = null, Action<StreamWriter> onInput = null, Action<string> onOutput = null, Action<string> onError = null, int timeoutMS = 0)
        {
            StringBuilder errorBuilder = new StringBuilder();
            StringBuilder dataBuilder = new StringBuilder();

            int exitCode = Run(workDir, file, timeoutMS, args,
                o =>
                {
                    onOutput?.Invoke(o);
                    if (dataBuilder.Length > 0)
                        dataBuilder.Append("\n");
                    dataBuilder.Append(o);

                },
                o =>
                {
                    onError?.Invoke(o);
                    if (errorBuilder.Length > 0)
                        errorBuilder.Append("\n");
                    errorBuilder.Append(o);
                }, onInput);

            if (exitCode != 0)
            {
                throw new Exception(errorBuilder.ToString());
            }

            return dataBuilder.ToString();
        }

        public static int Run(string workDir, string file, int timeoutMS, IEnumerable<string> args, Action<string> onOutput, Action<string> onError, Action<StreamWriter> onInput = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = file;
            startInfo.UseShellExecute = false;
            if (!string.IsNullOrEmpty(workDir))
                startInfo.WorkingDirectory = Path.GetFullPath(workDir);
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;

            if (args != null)
            {
                foreach (var arg in args)
                {
                    startInfo.ArgumentList.Add(arg);
                }
            }
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.GetEncoding("gb2312");
            startInfo.StandardErrorEncoding = Encoding.GetEncoding("gb2312");
            if (onInput != null)
            {
                startInfo.RedirectStandardInput = true;
                //startInfo.StandardInputEncoding = Encoding.UTF8;
            }

            using (var proc = new Process())
            {
                proc.StartInfo = startInfo;

                proc.OutputDataReceived += (o, e) =>
                {
                    if (e.Data != null)
                    {
                        //string text = Encoding.UTF8.GetString(Encoding.Default.GetBytes(e.Data));
                        //string text = e.Data;

                        onOutput?.Invoke(e.Data);
                    }
                };
                proc.ErrorDataReceived += (o, e) =>
                {
                    if (e.Data != null)
                    {
                        //string text = Encoding.UTF8.GetString(Encoding.Default.GetBytes(e.Data));

                        onError?.Invoke(e.Data);
                    }
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                if (onInput != null)
                {
                    onInput(proc.StandardInput);
                }

                if (timeoutMS > 0)
                {
                    proc.WaitForExit(timeoutMS);
                }
                else
                {
                    proc.WaitForExit();
                }
                try
                {
                    if (proc.ExitCode == 0)
                    {
                        string end = proc.StandardOutput.ReadToEnd();
                        if (!string.IsNullOrEmpty(end))
                            onOutput?.Invoke(end);
                    }
                    else
                    {
                        string end = proc.StandardError.ReadToEnd();
                        if (!string.IsNullOrEmpty(end))
                            onError?.Invoke(end);
                    }
                }
                catch
                {
                }

                if (!proc.HasExited)
                {
                    proc.Kill();
                }

                return proc.ExitCode;
            }
        }

        public const int DefaultTimeoutMS = 10 * 1000;



        internal static string RunCmdWithResult(string command, IEnumerable<string> args = null, string workDir = null, int timeoutMS = 0, Action<string> onOutput = null, Action<string> onError = null)
        {
            var _args = new List<string>() { "/C", command };
            if (args != null)
            {
                _args.AddRange(args);
            }

            return RunWithResult("cmd", args: _args, workDir: workDir, timeoutMS: timeoutMS, onOutput: onOutput, onError: onError);
        }

        internal static string RunCmd(Action<StreamWriter> onInput, string workDir = null, int timeoutMS = 0, Action<string> onOutput = null, Action<string> onError = null)
        {
            return RunWithResult("cmd", onInput: onInput, workDir: workDir, timeoutMS: timeoutMS, onOutput: onOutput, onError: onError);
        }

        public static Dictionary<string, string> GetLinkDirOrFiles(string dir)
        {
            if (string.IsNullOrEmpty(dir))
                dir = ".";

            string result = null;
            Dictionary<string, string> dic = new();
            try
            {
                result = RunCmdWithResult("dir", new string[] { "/AL" }, workDir: dir);
            }
            catch (Exception e)
            {
                //忽略，找不到文件
                //if (e.ErrorCode == 1)
                {
                    return dic;
                }
                //throw;
            }


            string pattern = "<(?<type>.+)>\\s+(?<from>.+)\\s+\\[(?<to>.+)\\]";

            foreach (Match m in Regex.Matches(result, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                string linkType = m.Groups["type"].Value;
                string fromPath = m.Groups["from"].Value.Trim();
                string toPath = m.Groups["to"].Value.Trim();
                fromPath = Path.Combine(dir, fromPath);
                toPath = Path.Combine(dir, toPath);
                switch (linkType)
                {
                    case "JUNCTION":
                        break;
                }
                dic[fromPath] = toPath;
            }
            return dic;
        }

        public static bool PathEqual(string a, string b)
        {
            a = NormalPath(a);
            b = NormalPath(b);
            return a == b;
        }

        public static bool IsLinkPackage(PackageInfo packageInfo)
        {
            return GetLinkPackagePath(packageInfo) != null;
        }

        public static bool IsLinkPackageByPath(string path)
        {
            return GetLinkPackagePathByPath(path) != null;
        }

        public static string GetLinkPackagePath(PackageInfo packageInfo)
        {
            string dir = packageInfo.FullDir;
            if (dir != null)
            {
                return GetLinkPackagePathByPath(dir);
            }
            return null;
        }

        public static string GetLinkPackagePathByPath(string path)
        {
            foreach (var item in GetOrCacheLinkPackages())
            {
                if (PathEqual(item.Item2, path))
                {
                    return item.Item1.path;
                }
            }
            return null;
        }

        internal static List<(PackageInfo, string)> cachedLinkPackages;

        public static void Refresh()
        {
            dependencies = null;
            cachedLinkPackages = null;
        }

        public static List<(PackageInfo, string)> GetOrCacheLinkPackages()
        {
            if (cachedLinkPackages == null)
            {
                cachedLinkPackages = FindLinkPackages("Packages");
            }

            return cachedLinkPackages;
        }

        public static List<(PackageInfo, string)> FindLinkPackages(string dir)
        {
            List<(PackageInfo, string)> packages = new();
            foreach (var item in GetLinkDirOrFiles(dir))
            {
                string path = item.Key;
                if (!Directory.Exists(path))
                    continue;
                PackageInfo info = PackageInfo.TryParse(path);
                if (info == null)
                    continue;
                info.path = path;
                packages.Add((info, item.Value));
            }
            return packages;
        }


        public static string CreateLinkPackage(string name)
        {
            var pkg = GetStarPackageInfo(name);
            if (pkg == null)
                throw new Exception($"Not found star package '{name}'");
            return CreateLinkPackage(pkg);
        }

        public static string CreateLinkPackage(PackageInfo packageInfo)
        {
            if (packageInfo == null)
                throw new ArgumentNullException(nameof(packageInfo));
            string targetDir = packageInfo.FullDir;
            if (string.IsNullOrEmpty(targetDir))
                throw new Exception($"Not local package '{packageInfo.name}'");
            targetDir = NormalPath(targetDir);

            string sourceRoot = Path.GetFullPath("Packages");
            string sourceDir = null;

            sourceDir = GetLinkPackagePath(packageInfo);

            if (sourceDir != null)
            {
                Log($"Already exists link package '{packageInfo.name}', path: '{sourceDir}', target: '{targetDir}'");
                return sourceDir;
            }

            sourceDir = Path.Combine(sourceRoot, Path.GetFileName(targetDir));
            //sourceDir = NormalPath(sourceDir);
            //不支持链接相对路径
            //targetDir = ToRelativePath(targetDir, Path.GetDirectoryName(sourceDir));

            if (Directory.Exists(sourceDir))
                throw new Exception($"Directory exists '{sourceDir}'");
            CreateLinkDir(sourceDir, targetDir);
            cachedLinkPackages = null;
            Log($"Create link package '{packageInfo.name}', path: '{sourceDir}', target: '{targetDir}'");
            return sourceDir;
        }

        public static string DeleteLinkPackage(PackageInfo packageInfo)
        {
            string path = GetLinkPackagePath(packageInfo);
            if (path == null)
            {
                return null;
            }
            if (Directory.Exists(path))
            {
                DeleteLink(path);
                cachedLinkPackages = null;
                Log($"Remove Link Package: '{packageInfo.name}', path: {path}");
                return path;
            }
            return null;
        }


        internal static void DeleteLink(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, false);

            }
        }


        //mklink /J 不支持相对路径
        public static void CreateLinkDir(string path, string target)
        {
            path = NormalPathLocal(path);
            target = NormalPathLocal(target);

            string absPath = NormalPathLocal(Path.GetFullPath(path));

            string parent = Path.GetDirectoryName(absPath);
            bool exists = false;
            if (Directory.Exists(parent))
            {
                foreach (var item in GetLinkDirOrFiles(parent))
                {
                    if (NormalPathLocal(item.Key) == absPath)
                    {
                        if (NormalPathLocal(item.Value) == target)
                        {
                            exists = true;
                        }
                        break;
                    }

                }
            }
            else
            {
                Directory.CreateDirectory(parent);
            }

            if (exists)
                return;
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            string result = RunCmdWithResult("mklink", new string[] { "/J", path, target });
            if (!Directory.Exists(path))
                throw new Exception("Link dir error");
        }

        //public static List<PackageInfo> FindProjectPackages()
        //{

        //}

        //public class CommandException : Exception
        //{
        //    public CommandException(int errorCode, string error)
        //    {
        //        ErrorCode = errorCode;
        //        Error = error;
        //    }

        //    public int ErrorCode { get; private set; }

        //    public string Error { get; private set; }

        //    public override string Message
        //    {
        //        get
        //        {
        //            if (!string.IsNullOrEmpty(Error))
        //                return $"ErrorCode: {ErrorCode}, Error: {Error}";
        //            return $"ErrorCode: {ErrorCode}";
        //        }
        //    }

        //}

        public static NpmAuth GetNpmAuth()
        {
            NpmAuth auth = null;
            if (!string.IsNullOrEmpty(EditorPackageSettings.NpmAddress) && !string.IsNullOrEmpty(EditorPackageSettings.NpmAuthToken))
            {
                auth = new NpmAuth()
                {
                    address = EditorPackageSettings.NpmAddress,
                    port = EditorPackageSettings.NpmPort,
                    authToken = EditorPackageSettings.NpmAuthToken
                };
            }

            if (auth == null && !string.IsNullOrEmpty(EditorPackageSettings.GlobalNpmAddress) && !string.IsNullOrEmpty(EditorPackageSettings.GlobalNpmAuthToken))
            {
                auth = new NpmAuth()
                {
                    address = EditorPackageSettings.GlobalNpmAddress,
                    port = EditorPackageSettings.GlobalNpmPort,
                    authToken = EditorPackageSettings.GlobalNpmAuthToken
                };
            }

            if (auth != null)
            {
                auth.url = $"http://{auth.address}:{auth.port}";
            }
            return auth;
        }

        public static NpmAuth GetNpmAuth(string address, int port)
        {
            NpmAuth auth = null;

            if (!string.IsNullOrEmpty(EditorPackageSettings.NpmAddress) && !string.IsNullOrEmpty(EditorPackageSettings.NpmAuthToken))
            {
                if (address == EditorPackageSettings.NpmAddress && port == EditorPackageSettings.NpmPort)
                {
                    auth = new NpmAuth()
                    {
                        address = EditorPackageSettings.NpmAddress,
                        port = EditorPackageSettings.NpmPort,
                        authToken = EditorPackageSettings.NpmAuthToken
                    };
                }
            }

            if (auth == null && !string.IsNullOrEmpty(EditorPackageSettings.GlobalNpmAddress) && !string.IsNullOrEmpty(EditorPackageSettings.GlobalNpmAuthToken))
            {
                if (address == EditorPackageSettings.GlobalNpmAddress && port == EditorPackageSettings.GlobalNpmPort)
                {
                    auth = new NpmAuth()
                    {
                        address = EditorPackageSettings.GlobalNpmAddress,
                        port = EditorPackageSettings.GlobalNpmPort,
                        authToken = EditorPackageSettings.GlobalNpmAuthToken
                    };
                }
            }

            return auth;
        }


        public static async Task NpmCmd(NpmAuth auth, string packageDir, Action<string, string> action)
        {
            if (string.IsNullOrEmpty(packageDir))
                throw new ArgumentNullException(nameof(packageDir));
            if (!Directory.Exists(packageDir))
                throw new DirectoryNotFoundException($"Package Dir not exists '{packageDir}'");

            string address = auth.address;
            int port = auth.port;
            string authToken = auth.authToken;

            if (string.IsNullOrEmpty(address))
                throw new Exception("Npm Address null");

            if (string.IsNullOrEmpty(authToken))
                throw new Exception("Npm AuthToken null");

            string registryUrl;
            registryUrl = $"http://{address}:{port}";


            await Task.Run(() =>
            {
                //try
                //{
                string text = RunCmdWithResult("npm",
                    new string[] { "set", $"//{address}:{port}/:_authToken", authToken },
                    timeoutMS: 10 * 1000);

                action?.Invoke(packageDir, registryUrl);
                //}
                //catch (Exception ex)
                //{
                //    Debug.LogError(ex.Message);
                //}
            });

        }




        public static async Task<string> PublishPackage(string packageDir)
        {
            var win = PublishPackageWindow.OpenWinow(packageDir);
            return win.packageInfo.version;
        }


        public static async Task<string> PublishPackage(NpmAuth auth, string packageDir, string newVersion, bool createTag = false, bool push = false)
        {
            if (!Directory.Exists(packageDir))
                throw new DirectoryNotFoundException(packageDir);

            PackageInfo packageInfo = PackageInfo.TryParse(packageDir);
            if (packageInfo == null)
                throw new Exception("PackageInfo null");

            GitRepository git = null;
            try
            {
                using (var bar = new EditorProgressBar($"Publish Package {packageInfo.name}", null, 0f))
                {
                    var version = packageInfo.Version;
                    string tagName = null;

                    if (!string.IsNullOrEmpty(newVersion))
                    {
                        packageInfo.Version = newVersion;
                        version = packageInfo.Version;
                    }

                    //检查是否存在git
                    if (GitUtility.IsInsideRepositoryDir(packageDir))
                    {
                        git = new GitRepository(packageDir);
                        git.BeginDirectory(".");

                        //检查包目录是否有修改
                        var changedFiles = git.GetChangedFiles();
                        if (changedFiles.Length > 0)
                        {
                            throw new Exception($"Package Directory has files({changedFiles.Length}) changed:\n{string.Join("\n", changedFiles.Take(3))}");
                        }
                    }

                    //检查最近的tag是否有提交变化
                    if (git != null)
                    {
                        var tags = git.GetVersionTags(sortAsc: false, limit: 1);
                        if (tags.Length > 0)
                        {
                            string lastTagCommitId = git.GetTagInfo(tags[0]).commitId;
                            var commit = git.GetCommit();
                            if (commit.id == lastTagCommitId)
                            {
                                throw new Exception("Not changed");
                            }
                        }
                    }

                    if (createTag)
                    {
                        tagName = $"v{version}";
                        if (git.GetTags(tagName).Any(o => o == tagName))
                        {
                            throw new Exception($"Exists tag '{tagName}'");
                        }
                    }

                    //写入版本号
                    PackageInfo.WriteProperty(packageInfo.FilePath, PackageInfo.Properties.Version, version);

                    //提交版本号
                    if (git.GetChangedFiles().Contains(PackageInfo.FILE_NAME))
                    {
                        string commitMsg = $"v{packageInfo.Version}";
                        git.AddToCommit(PackageInfo.FILE_NAME);
                        git.Commit(commitMsg);
                    }

                    //创建版本标签
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        git.CreateTag(tagName);
                        if (push)
                        {
                            string remote = git.GetDefaultRemote();
                            git.PushTag(remote, tagName);
                        }
                    }



                    await NpmCmd(auth, packageInfo.FullDir, (dir, registryUrl) =>
                    {
                        string text = RunCmdWithResult("npm",
                            new string[] { "publish", "--registry", registryUrl },
                            workDir: dir,
                            timeoutMS: 10 * 1000,
                            onOutput: o =>
                            {
                                bar.Message = o;
                            }, onError: o =>
                            {
                                bar.Message = o;
                            });

                        if (!string.IsNullOrEmpty(text))
                        {
                            Debug.Log(text);
                        }
                        Debug.Log($"Publish Package Success, Name: '{packageInfo.name}', Version: '{packageInfo.version}'");

                    });

                }
            }
            finally
            {
                if (git != null)
                {
                    git.Dispose();
                    git = null;
                }
            }

            return packageInfo.version;
        }
        public static Task UnpublishPackage(NpmAuth auth, PackageInfo packageInfo)
        {
            return UnpublishPackage(auth, packageInfo.FullDir, packageInfo.name, packageInfo.version);
        }
        public static async Task UnpublishPackage(NpmAuth auth, string packageDir, string name, string version)
        {
            using (var bar = new EditorProgressBar($"Unpublish Package {name}@{version}", null, 0f))
            {
                await NpmCmd(auth, packageDir, (dir, registryUrl) =>
                {//, "--force"
                    string text = RunCmdWithResult("npm",
                        new string[] { "unpublish", $"{name}@{version}", "--registry", registryUrl },
                        workDir: dir,
                        timeoutMS: 10 * 1000,
                        onOutput: o =>
                        {
                            bar.Message = o;
                        }, onError: o =>
                        {
                            bar.Message = o;
                        });

                    //if (!string.IsNullOrEmpty(text))
                    //{
                    //    Debug.Log(text);
                    //}
                    Debug.Log($"Unpublish Package Success, Name: '{name}', Version: '{version}'");

                });
            }
        }
        static string FixPacakgeCompilePathKey = $"{EditorPackageUtility.UnityPackageName}:FixPacakgeCompilePath";
        public static void FixPacakgeCompileError(string packageName)
        {
            string dir = Directory.GetDirectories("Library/PackageCache", packageName + "@*").FirstOrDefault();
            if (string.IsNullOrEmpty(dir))
            {
                Debug.LogError($"PackageCache not found package: {packageName}");
                return;
            }
            string folderName = Path.GetFileName(dir);
            string targetDir;
            targetDir = Path.Combine(Path.Combine("Packages", folderName));

            if (Directory.Exists(targetDir))
            {
                Debug.LogError($"Already exists directory '{targetDir}'");
                return;
            }

            PlayerPrefs.SetString(FixPacakgeCompilePathKey, targetDir);
            PlayerPrefs.Save();
            FileUtil.CopyFileOrDirectory(dir, targetDir);
            EditorPackageUtility.DebugLog($"Fix Package Compile error, Copy Path: {targetDir}");
            RefreshProject();
        }

        [InitializeOnLoadMethod]
        static void InitializeOnLoadMethod()
        {
            string targetDir = PlayerPrefs.GetString(FixPacakgeCompilePathKey, null);

            if (!string.IsNullOrEmpty(targetDir))
            {
                PlayerPrefs.DeleteKey(FixPacakgeCompilePathKey);
                PlayerPrefs.Save();
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                    EditorPackageUtility.DebugLog($"Fix Package Compile error, Delete Path: {targetDir}");
                    RefreshProject();
                }
            }
        }

        internal static NamedBuildTarget BuildTargetGroupToNamedBuildTarget(BuildTargetGroup buildTargetGroup)
        {
            NamedBuildTarget namedBuildTarget;
            if (buildTargetGroup == BuildTargetGroup.Standalone && EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Server)
            {
                namedBuildTarget = NamedBuildTarget.Server;
            }
            else
            {
                namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            }
            return namedBuildTarget;
        }

        internal static NamedBuildTarget CurrentNamedBuildTarget
        {
            get
            {
                var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
                return BuildTargetGroupToNamedBuildTarget(buildTargetGroup);
            }
        }

        internal static string[] GetDefineSymbols()
            => GetDefineSymbols(CurrentNamedBuildTarget);


        internal static string[] GetDefineSymbols(BuildTargetGroup buildTargetGroup)
        {
            var namedBuildTarget = BuildTargetGroupToNamedBuildTarget(buildTargetGroup);
            return GetDefineSymbols(namedBuildTarget);
        }

        internal static string[] GetDefineSymbols(NamedBuildTarget namedBuildTarget)
        {
            string[] defines;
            PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget, out defines);
            return defines;
        }

        internal static bool HasDefineSymbols(NamedBuildTarget namedBuildTarget, params string[] defines)
        {
            var current = GetDefineSymbols(namedBuildTarget);
            return defines.All(o => current.Contains(o));
        }

        internal static bool HasDefineSymbols(params string[] defines)
            => HasDefineSymbols(CurrentNamedBuildTarget, defines);

        internal static void AddDefineSymbols(params string[] defines)
            => AddDefineSymbols(CurrentNamedBuildTarget, defines);

        internal static bool AddDefineSymbols(NamedBuildTarget namedBuildTarget, params string[] defines)
        {
            List<string> list = new List<string>(GetDefineSymbols());
            bool changed = false;
            foreach (var define in defines)
            {
                if (!list.Contains(define))
                {
                    list.Add(define);
                    changed = true;
                }
            }
            if (changed)
            {
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, list.ToArray());
            }
            return changed;
        }


        internal static bool RemoveDefineSymbols(params string[] defines)
            => RemoveDefineSymbols(CurrentNamedBuildTarget, defines);

        internal static bool RemoveDefineSymbols(NamedBuildTarget namedBuildTarget, params string[] defines)
        {
            List<string> list = new List<string>(GetDefineSymbols());
            bool changed = false;
            foreach (var define in defines)
            {
                if (list.Contains(define))
                {
                    list.Remove(define);
                    changed = true;
                }
            }
            if (changed)
            {
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, list.ToArray());
            }
            return changed;
        }


        public static Assembly[] FindPackageAssemblies(string packageName)
        {
            List<Assembly> list = new List<Assembly>();
            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                string pkgName = GetPackageName(ass);
                if (string.IsNullOrEmpty(pkgName))
                    continue;
                if (pkgName == packageName)
                {
                    list.Add(ass);
                }
            }
            return list.ToArray();
        }

        public static Dictionary<string, string[]> GetPackageAssemblyDefines(string packageName)
        {
            Dictionary<string, string[]> defines = new();
            foreach (var assembly in FindPackageAssemblies(packageName))
            {
                defines[assembly.GetName().Name] = GetAssemblyDefines(assembly);
            }
            return defines;
        }

        internal static string[] GetAssemblyDefines(Assembly assembly)
        {
            foreach (var item in GetAssemblyMetadatas(assembly))
            {
                if (item.Key == "Define")
                {
                    return item.Value.Distinct().OrderBy(o => o).ToArray();
                }
            }
            return new string[0];
        }

        public static AssemblyDefinitionInfo[] FindPackageAssemblyDefinitions(string path)
        {
            return AssemblyDefinitionInfo.Find(path);
        }

        internal static Dictionary<string, string[]> GetPackageDefineConstraints(string path)
        {
            Dictionary<string, string[]> defines = new();
            foreach (var asmDef in FindPackageAssemblyDefinitions(path))
            {
                List<string> asmDefines = new List<string>();
                if (asmDef.defineConstraints != null)
                {
                    foreach (var define in asmDef.defineConstraints)
                    {
                        if (!asmDefines.Contains(define))
                        {
                            asmDefines.Add(define);
                        }
                    }
                }

                defines[asmDef.name] = asmDefines.ToArray();

            }
            return defines;
        }


        public static void RefreshProject()
        {
            EditorApplication.ExecuteMenuItem("Assets/Refresh");
            //AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            //EditorUtility.RequestScriptReload();
        }


        public static void OpenPackageManifest()
        {
            string path = PackageManifestPath;
            path = Path.GetFullPath(path);
            if (!File.Exists(path))
            {
                return;
            }
            Application.OpenURL(path);
        }

        public static PackageInfo GetProjectPackage(string name)
        {
            foreach (var pkg in ProjectRepsitory.GetPackages())
            {
                if ((pkg.flags & PackageFlags.PackageCache) == PackageFlags.PackageCache)
                    continue;
                if (pkg.name == name)
                    return pkg;
            }
            return null;
        }

        public static bool IsProjectPackage(string name)
        {
            return GetProjectPackage(name) != null;
        }


        public static bool IsGitRootDir(string dir)
        {
            if (string.IsNullOrEmpty(dir))
                return false;

            return Directory.Exists(Path.Combine(dir, ".git"));
        }

    }


    class EditorProgressBar : IDisposable
    {
        private string title;
        private string message;
        private float progress;
        private bool diried;
        private CallbackFunction update;

        public EditorProgressBar(string title, string message, float progress)
        {
            this.title = title;
            this.message = message;
            this.progress = progress;
            diried = true;
            EditorApplication.update += Update;
        }

        public string Title
        {
            get => title;
            set
            {
                if (title != value)
                {
                    title = value;
                    diried = true;
                }

            }
        }
        public string Message
        {
            get => message;
            set
            {
                if (message != value)
                {
                    message = value;
                    diried = true;
                }
            }
        }

        public float Progress
        {
            get => progress;
            set
            {
                if (progress != value)
                {
                    progress = value;
                    diried = true;
                }
            }
        }

        void Update()
        {
            if (!diried)
            {
                return;
            }
            diried = false;
            string title = this.title;
            string msg = this.message;
            float progress = this.progress;
            EditorUtility.DisplayProgressBar(title, msg, progress);
        }


        public void Dispose()
        {
            EditorApplication.update -= Update;
            EditorUtility.ClearProgressBar();
        }



    }

    [Serializable]
    public class NpmAuth
    {
        public string address;
        public ushort port;
        public string authToken;
        public string url;
    }

}