using GluonGui.WorkspaceWindow.Views.WorkspaceExplorer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.PackageManagement
{

    [Serializable]
    public class PackageRepository : ISerializationCallbackReceiver
    {
        public string name;
        public string url;

        public List<string> excludeNames = new();

        public List<string> excludePaths = new();


        [HideInInspector]
        public List<PackageInfo> packages = new();

        [HideInInspector]
        public List<FavoritePackage> favorites = new();

        [NonSerialized]
        public string localDir;
        [NonSerialized]
        public bool editable = true;

        public bool IsLocal => !string.IsNullOrEmpty(localDir);
        internal bool loaded;

        [NonSerialized]
        public PackageRepository reference;

        public List<PackageInfo> GetPackages()
        {
            if (reference != null)
            {
                return reference.GetPackages();
            }


            return packages;
        }

        public IEnumerable<FavoritePackage> GetMissingFavorites()
        {
            if (reference != null)
            {
                foreach (var item in reference.GetMissingFavorites())
                {
                    yield return item;
                }
                yield break;
            }

            foreach (var item in favorites)
            {
                if (!packages.Any(o => item.Equals(o)))
                {
                    yield return item;
                }
            }
        }

        public bool IsProject
        {
            get
            {
                if (string.IsNullOrEmpty(localDir))
                    return false;
                string fullPath = EditorPackageUtility.NormalPath(Path.GetFullPath(localDir));
                bool isProject = false;
                if (EditorPackageUtility.NormalPath(Environment.CurrentDirectory) == fullPath)
                {
                    isProject = true;
                }
                return isProject;
            }
        }

        public void ScanLocalPackages()
        {
            packages.Clear();

            if (!string.IsNullOrEmpty(localDir))
            {
                if (!Directory.Exists(localDir))
                {
                    return;
                }
                string fullPath = EditorPackageUtility.NormalPath(Path.GetFullPath(localDir));
                bool isProject = IsProject;
        
                int startIndex = fullPath.Length;
                if (!fullPath.EndsWith("/"))
                    startIndex++;


                List<Regex> excludeNameRegex = null;
                List<Regex> excludePathRegex = new();

                if (excludeNames != null)
                {
                    foreach (var pattern in excludeNames)
                    {
                        if (excludeNameRegex == null)
                        {
                            excludeNameRegex = new();
                        }
                        excludeNameRegex.Add(new Regex(pattern, RegexOptions.IgnoreCase));
                    }
                }
                if (excludePaths != null)
                {
                    foreach (var pattern in excludePaths)
                    {
                        excludePathRegex.Add(new Regex(pattern, RegexOptions.IgnoreCase));
                    }
                }

                if (isProject)
                {
                    excludePathRegex.Add(new Regex("/Library/PackageCache/", RegexOptions.IgnoreCase));
                    excludePathRegex.Add(new Regex("^(Temp|UserSettings|ProjectSettings|Logs)/", RegexOptions.IgnoreCase));
                }
                else
                {
                    excludePathRegex.Add(new Regex("(^|/)Library/PackageCache/", RegexOptions.IgnoreCase));
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                HashSet<string> packageDirs = new();

                FindPackageDir(fullPath, startIndex, excludePathRegex, packageDirs);

                //string[] packageDirs2 = packageDirs.ToArray();
                //Parallel.ForEach(packageDirs,
                //    dir =>
                //{


                foreach (var dir in packageDirs)
                {
                    string pkgFilePath = Path.Combine(dir, "package.json");
                    //    foreach (var path in Directory.GetFiles(fullPath, "package.json", SearchOption.AllDirectories))
                    //{

                    try
                    {
                        //var path2 = EditorPackageUtility.NormalPath(path.Substring(startIndex));
                        string location = EditorPackageUtility.NormalPath(dir.Substring(startIndex));
                        //var parentDir = EditorPackageUtility.NormalPath(Path.GetDirectoryName(location));

                        //if (excludePathRegex != null && excludePathRegex.Any(o => o.IsMatch(path2)))
                        //    continue;

                        //if (EditorPackageUtility.IsSymbolicLink(Path.GetDirectoryName(path), true))
                        //    continue;

                        var pkg = PackageInfo.TryParse(dir);
                        if (pkg == null)
                            continue;

                        if (excludeNameRegex != null && excludeNameRegex.Any(o => o.IsMatch(pkg.name)))
                            continue;

                        pkg.path = location;
                        pkg.owner = this;

                        pkg.dependencies = EditorPackageUtility.GetPackageDependencies(pkgFilePath).ToArray();

                        if (isProject && location.StartsWith("Library/PackageCache/"))
                        {
                            pkg.flags |= PackageFlags.PackageCache;
                        }
                        //Debug.Log("Package: " + pkg.name + ", " + pkg.flags + ", " + pkg.path);
                        lock (packages)
                        {
                            packages.Add(pkg);
                        }
                    }
                    catch { }
                }
                //});
                EditorPackageUtility.DebugLog($"Find Package {packages.Count} ({stopwatch.Elapsed.TotalSeconds:0.#}s)");
            }


            packages.Sort((a, b) => a.version.CompareTo(b.version));
            packages.Sort((a, b) => a.name.CompareTo(b.name));
        }

        void FindPackageDir(string path, int subDirIndex, List<Regex> excludePathRegex, HashSet<string> result)
        {
            if (EditorPackageUtility.IsSymbolicLink(path, false))
                return;

            if (path.Length > subDirIndex)
            {
                var path2 = EditorPackageUtility.NormalPath(path.Substring(subDirIndex));

                //if (path2.EndsWith("Library/PackageCache"))
                //    return;

                if (excludePathRegex != null && excludePathRegex.Any(o => o.IsMatch(path2)))
                    return;

            }
            else
            {

            }

            if (File.Exists(Path.Combine(path, "package.json")))
            {
                lock (result)
                {
                    result.Add(path);
                }
                return;
            }

            Parallel.ForEach(Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly),
                (dir) =>
                {
                    FindPackageDir(dir, subDirIndex, excludePathRegex, result);
                });

            //foreach(var dir in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
            //{
            //    FindPackageDir(dir, subDirIndex, excludePathRegex, result);
            //}
        }

        void UpdateUrl()
        {
            localDir = null;

            if (!string.IsNullOrEmpty(url))
            {
                if (url.IndexOf("://") >= 0)
                {
                    var uri = new Uri(url);
                    if (uri.Scheme == "file")
                    {
                        localDir = uri.LocalPath;
                    }
                }
                else
                {
                    localDir = url;
                }
            }
        }

        public void Update()
        {
            UpdateUrl();

            bool isProject = IsProject;
            foreach (var pkg in packages)
            {
                pkg.flags &= PackageFlags.Saved;


                string dir = pkg.FullDir;
                
                if (!string.IsNullOrEmpty(dir))
                {
                    PackageInfo.TryParse(dir, pkg);
                    pkg.flags |= PackageFlags.Local;
                }

                if (IsFavorite(pkg))
                {
                    pkg.flags |= PackageFlags.Favorite;
                }
                
                if ((pkg.flags & PackageFlags.Local) == PackageFlags.Local)
                {
                    if (EditorPackageUtility.HasManifestPackage(pkg.name, pkg.GetManifestUri()))
                    {
                        pkg.flags |= PackageFlags.LocalUsed;
                    }
                    if (EditorPackageUtility.IsLinkPackage(pkg))
                    {
                        pkg.flags |= PackageFlags.LinkUsed;
                    }

                    //if (pkg.path.StartsWith("Library/PackageCache/"))
                    //{
                    //    pkg.flags |= PackageFlags.PackageCache;
                    //}
                }
                
                string verStr = EditorPackageUtility.GetManifestVersion(pkg.name);
                if (verStr != null)
                {
                    pkg.flags |= PackageFlags.ManifestUsed;

                    if (Version.TryParse(verStr, out var v))
                    {
                        pkg.flags |= PackageFlags.VersionUsed;
                    }
                }

                if (isProject)
                {
                    if ((pkg.flags & PackageFlags.PackageCache) != PackageFlags.PackageCache)
                    {
                        pkg.flags |= PackageFlags.ProjectUsed;
                    }
                }
                else if (EditorPackageUtility.IsProjectPackage(pkg.name))
                {
                    pkg.flags |= PackageFlags.ProjectUsed;
                }

            }


            if (reference != null)
            {
                reference.Update();
            }

        }

        public void OnAfterDeserialize()
        {
            foreach (var pkg in packages)
            {
                pkg.owner = this;
            }
            UpdateUrl();
        }

        public void OnBeforeSerialize()
        {

        }

        public void ShowInspector()
        {
            var inspectorObject = ScriptableObject.CreateInstance<InspectorObject>();
            inspectorObject.target = this;
            inspectorObject.hideFlags = HideFlags.DontSave;
            //inspectorEditor = UnityEditor.Editor.CreateEditor(inspectorObject);
            //inspectorEditor.hideFlags = HideFlags.DontSave;
            Selection.activeObject = inspectorObject;
        }

        internal class InspectorObject : ScriptableObject
        {
            public PackageRepository target;
        }

        public static PackageRepository LoadLocal(string path)
        {
            PackageRepository repository = null;

            string repoPath = Path.Combine(GetLocalRepositoryDir(path), "Repository.json");
            try
            {
                if (File.Exists(repoPath))
                {
                    string json = File.ReadAllText(repoPath, Encoding.UTF8);
                    repository = JsonUtility.FromJson<PackageRepository>(json);
                    repository.loaded = true;
                }
            }
            catch { }

            if (repository == null)
                repository = new PackageRepository();
            repository.url = path;
            repository.localDir = path;
            return repository;
        }


        public void SaveLocal(string path)
        {
            string repoPath = Path.Combine(GetLocalRepositoryDir(path), "Repository.json");
            var oldUrl = url;
            url = null;
            string json = JsonUtility.ToJson(this, true);
            url = oldUrl;
            if (File.Exists(repoPath))
            {
                string old = File.ReadAllText(repoPath, Encoding.UTF8);
                if (old == json)
                {
                    return;
                }
            }

            EditorPackageUtility.DebugLog($"Save Local Package Repository: '{name}', path: {repoPath}");
            Directory.CreateDirectory(Path.GetDirectoryName(repoPath));
            File.WriteAllText(repoPath, json, Encoding.UTF8);
        }

        public void Save()
        {

            if (IsLocal)
            {
                if (reference != null)
                {
                    reference.SaveLocal(localDir);
                }
                else
                {
                    SaveLocal(localDir);
                }
            }
            else
            {
                EditorPackageUtility.DebugLog($"Save Package Repository: '{name}'");
                EditorPackageSettings.Save();
            }
        }

        public static string GetLocalRepositoryDir(string path)
        {
            return Path.Combine(path, ".packages");
        }

        public string GetLocalRepositoryDir()
        {
            if (IsLocal)
            {
                return GetLocalRepositoryDir(localDir);
            }
            return null;
        }

        public bool IsFavorite(PackageInfo package)
        {
            return favorites.Any(o => o.Equals(package));
        }

        public bool AddFavorite(PackageInfo package)
        {
            if (IsFavorite(package))
                return false;
            favorites.Add(new FavoritePackage()
            {
                name = package.name,
                displayName = package.displayName,
                version = package.version,
                path = package.path,
            });
            return true;
        }

        public bool RemoveFavorite(PackageInfo package)
        {
            for (var i = 0; i < favorites.Count; i++)
            {
                var item = favorites[i];
                if (item.name == package.name && item.path == package.path)
                {
                    favorites.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public string ShowInExplorer()
        {
            string path = GetLocalRepositoryDir();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                path = GetLocalRepositoryDir();
                EditorUtility.RevealInFinder(path);
                return path;
            }
            else
            {
                path = localDir;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    EditorUtility.RevealInFinder(path);
                    return path;
                }
            }
            return null;
        }

        public override string ToString()
        {
            return $"{name}";
        }

    }



}
