using Codice.CM.Common.Serialization.Replication;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Git;
using Unity.SettingsManagement.Editor;

namespace Unity.PackageManagement
{
    class PackageSettingsProvider : UnityEditor.SettingsProvider
    {
        private VisualElement root;
        private ListView repositoryListView;
        private ListView packageListView;
        private string searchText;


        internal const string MenuPath = "Unity/Package Management";

        Dictionary<PackageInfo, PackageData> packageDatas = new();

        class PackageData
        {
            public bool changed;
        }

        public PackageSettingsProvider()
              : base(MenuPath, SettingsScope.Project)
        {

        }

        public PackageRepository SelectedRepository
        {
            get
            {
                PackageRepository repository = null;
                if (repositoryListView.selectedIndex >= 0)
                    repository = repositoryListView.itemsSource[repositoryListView.selectedIndex] as PackageRepository;
                return repository;
            }
        }

        [SettingsProvider]
        static UnityEditor.SettingsProvider CreateSettingsProvider()
        {
            var provider = new PackageSettingsProvider();
            provider.keywords = new string[] { "package" };
            return provider;
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            string docFile = Path.GetFullPath(Path.Combine(EditorPackageUtility.GetUnityPackageDirectory(EditorPackageUtility.UnityPackageName), "README.md"));
            var contentContainer = EditorSettingsUtility.CreateSettingsWindow(rootElement, "Package Management", scroll: false, helpLink: docFile);

            root = EditorPackageUtility.LoadUXML(typeof(PackageSettingsProvider), "PackageSettings", contentContainer);
            EditorPackageUtility.AddStyle(rootElement, typeof(PackageSettingsProvider), "PackageSettings");
            root.style.flexGrow = 1f;

            EditorPackageUtility.Refresh();

            var leftPanel = root.Q("left-panel");
            var rightPanel = root.Q("right-panel");
            repositoryListView = leftPanel.Q<ListView>();
            packageListView = rightPanel.Q<ListView>();

            var toolbar = leftPanel.Q<Toolbar>();
            var newButton = toolbar.Q<ToolbarButton>("new");
            var openManifestButton = rightPanel.Q<ToolbarButton>("open-manifest");
            var showInExplorerButton = rightPanel.Q<ToolbarButton>("show-in-explorer");
            var openRepoButton = rightPanel.Q<ToolbarButton>("open-repo-web");
            //var openManifestButton = rightPanel.Q<ToolbarMenu>("open-manifest");

            newButton.clicked += () =>
            {
                PackageRepository item = new PackageRepository();
                item.name = "New Repositoy";

                EditorPackageSettings.Repositories.Add(item);
                EditorPackageSettings.Repositories = EditorPackageSettings.Repositories;
                EditorPackageSettings.Save();

                LoadRepositoryList();
                repositoryListView.selectedIndex = repositoryListView.itemsSource.IndexOf(item);
            };

            toolbar = rightPanel.Q<Toolbar>();
            var refreshButton = toolbar.Q<ToolbarButton>("refresh");
            refreshButton.clicked += () =>
            {
                EditorPackageUtility.Refresh();

                PackageRepository item = SelectedRepository;
                if (item != null)
                {
                    RefreshRepository(item);
                    if (SelectedRepository == EditorPackageUtility.StarRepsitory)
                    {

                    }
                    else
                    {
                        LoadRepositoryList();
                    }
                }
            };

            toolbar.Q<ToolbarSearchField>().RegisterValueChangedCallback(e =>
            {
                searchText = e.newValue;
                LoadPackageList();
            });

            //openManifestButton.RegisterCallback<MouseDownEvent>(e => EditorPackageUtility.OpenPackageManifest());
            openManifestButton.clicked += EditorPackageUtility.OpenPackageManifest;
            showInExplorerButton.clicked += () =>
            {
                SelectedRepository?.ShowInExplorer();
            };

            openRepoButton.clicked += () =>
            {
                var auth = EditorPackageUtility.GetNpmAuth();
                if (auth != null && !string.IsNullOrEmpty(auth.url))
                {
                    Application.OpenURL(auth.url);
                }
            };

            repositoryListView.makeItem = () =>
            {
                VisualElement container = new VisualElement();
                container.AddToClassList("repository-item");

                Label nameLabel = new Label();
                nameLabel.AddToClassList("repository-item_name");
                container.Add(nameLabel);

                container.AddManipulator(new MenuManipulator(e =>
                {
                    var repo = container.userData as PackageRepository;

                    e.menu.AppendAction("Delete", act =>
                    {
                        if (EditorPackageSettings.Repositories.Remove(repo))
                        {
                            EditorPackageSettings.Save();
                        }
                        LoadRepositoryList();
                    }, act =>
                    {
                        if (!EditorPackageSettings.Repositories.Contains(repo))
                            return DropdownMenuAction.Status.Disabled;
                        return DropdownMenuAction.Status.Normal;
                    });

                    e.menu.AppendAction("Show In Explorer", act =>
                    {
                        repo.ShowInExplorer();
                    }, act =>
                    {
                        string path = repo.localDir;
                        if (!Directory.Exists(path))
                            return DropdownMenuAction.Status.Disabled;
                        return DropdownMenuAction.Status.Normal;
                    });

                    if (repo == EditorPackageUtility.ProjectRepsitory)
                    {
                        e.menu.AppendAction("Open PackageCache Folder", act =>
                        {
                            string path = Path.GetFullPath("Library/PackageCache");

                            if (Directory.Exists(path))
                            {
                                var firstDir = Directory.GetDirectories(path, "*").FirstOrDefault();
                                if (!string.IsNullOrEmpty(firstDir))
                                {
                                    EditorUtility.RevealInFinder(firstDir);
                                }
                                else
                                {
                                    EditorUtility.RevealInFinder(path);
                                }
                            }
                        }, act =>
                        {
                            string path = Path.GetFullPath("Library/PackageCache");
                            if (!Directory.Exists(path))
                                return DropdownMenuAction.Status.Disabled;
                            return DropdownMenuAction.Status.Normal;
                        });

                        e.menu.AppendAction("Open Manifest Folder", act =>
                        {
                            string path = Path.GetFullPath("Packages/manifest.json");
                            if (File.Exists(path))
                            {
                                EditorUtility.RevealInFinder(path);
                            }
                        }, act =>
                        {
                            string path = Path.GetFullPath("Packages/manifest.json");
                            if (!File.Exists(path))
                                return DropdownMenuAction.Status.Disabled;
                            return DropdownMenuAction.Status.Normal;
                        });


                        e.menu.AppendAction("Open Manifest", act =>
                        {
                            EditorPackageUtility.OpenPackageManifest();
                        }, act =>
                        {
                            string path = EditorPackageUtility.PackageManifestPath;
                            path = Path.GetFullPath(path);
                            if (!File.Exists(path))
                            {
                                return DropdownMenuAction.Status.Disabled;
                            }
                            return DropdownMenuAction.Status.Normal;
                        });
                    }

                }));

                return container;
            };

            repositoryListView.bindItem = (view, index) =>
            {
                var item = repositoryListView.itemsSource[index] as PackageRepository;
                view.userData = item;
                var nameLabel = view.Q<Label>(className: "repository-item_name");
                nameLabel.text = item.name;

            };
            repositoryListView.selectionChanged += (selected) =>
            {
                var item = selected.FirstOrDefault() as PackageRepository;
                if (item != null)
                {
                    item.ShowInspector();
                }
                LoadPackageList();
            };

            packageListView.makeItem = () =>
            {
                VisualElement container = new VisualElement();
                container.AddToClassList("package-item");

                Toggle check = new Toggle();
                check.AddToClassList("package-item_check");
                check.RegisterValueChangedCallback(e =>
                {
                    var pkg = container.userData as PackageInfo;

                    if (e.newValue)
                    {
                        if (pkg.FullDir != null)
                        {
                            EditorPackageUtility.AddManifestLocalPackage(pkg.name, pkg.GetManifestUri());
                        }
                        else
                        {
                            EditorPackageUtility.AddManifestPackage(pkg.name, pkg.GetManifestUri());
                        }
                    }
                    else
                    {
                        EditorPackageUtility.RemoveManifestPackage(pkg);
                    }
                    LoadPackageList();
                    //CompilationPipeline.RequestScriptCompilation();
                });
                container.Add(check);

                Label starLabel = new Label();
                starLabel.AddToClassList("package-item_star");
                starLabel.text = "★";
                starLabel.RegisterCallback<MouseDownEvent>(e =>
                {
                    var pkg = container.userData as PackageInfo;
                    if (pkg.IsFavorite)
                    {
                        pkg.owner.RemoveFavorite(pkg);
                    }
                    else
                    {
                        pkg.owner.AddFavorite(pkg);
                    }

                    EditorPackageUtility.DebugLog($"Favorite Package: {pkg.name}={pkg.IsFavorite}");
                    pkg.owner.Save();

                    LoadPackageList();
                }, TrickleDown.TrickleDown);
                container.Add(starLabel);

                Label nameLabel = new Label();
                nameLabel.AddToClassList("package-item_name");
                container.Add(nameLabel);

                Label pathLabel = new Label();
                pathLabel.AddToClassList("package-item_path");
                container.Add(pathLabel);


                Label codeLineLabel = new Label();
                codeLineLabel.AddToClassList("package-item_code-line");
                container.Add(codeLineLabel);

                Label versionLabel = new Label();
                versionLabel.AddToClassList("package-item_version");
                container.Add(versionLabel);

                container.AddManipulator(new MenuManipulator(e =>
                {
                    var pkg = container.userData as PackageInfo;
                    var menu = e.menu;
                    if (!string.IsNullOrEmpty(pkg.name))
                    {
                        menu.AppendAction("Version", act =>
                        {
                            if (EditorPackageUtility.HasManifestPackage(pkg.name, pkg.version))
                            {
                                EditorPackageUtility.RemoveManifestPackage(pkg.name);
                            }
                            else
                            {
                                EditorPackageUtility.AddManifestPackage(pkg.name, pkg.version);
                            }
                            LoadPackageList();

                        }, act =>
                        {
                            if (EditorPackageUtility.HasManifestPackage(pkg.name, pkg.version))
                            {
                                return DropdownMenuAction.Status.Checked;
                            }
                            return DropdownMenuAction.Status.Normal;
                        });
                    }

                    if ((pkg.flags & PackageFlags.Missing) != 0)
                        return;

                    if (!string.IsNullOrEmpty(pkg.path))
                    {
                        //e.menu.AppendAction("Exclude", act =>
                        //{
                        //    pkg.owner.excludePaths.Add(pkg.path);
                        //    LoadPackageList();
                        //}, pkg.owner.excludePaths.Contains(pkg.path) ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);



                        menu.AppendAction("Local", act =>
                        {
                            string dir = pkg.FullDir;
                            if (dir != null)
                            {
                                if (EditorPackageUtility.HasManifestPackage(pkg.name, pkg.GetManifestUri()))
                                {
                                    EditorPackageUtility.RemoveManifestPackage(pkg.name);
                                }
                                else
                                {
                                    EditorPackageUtility.AddManifestLocalPackage(pkg.name, pkg.GetManifestUri());
                                }
                                LoadPackageList();
                            }
                        }, act =>
                        {
                            string dir = pkg.FullDir;
                            if (dir != null)
                            {
                                if ((pkg.flags & PackageFlags.LocalUsed) == PackageFlags.LocalUsed)
                                {
                                    return DropdownMenuAction.Status.Checked;
                                }
                                return DropdownMenuAction.Status.Normal;
                            }
                            return DropdownMenuAction.Status.Disabled;
                        });

                        menu.AppendAction("Link", act =>
                        {
                            string dir = pkg.FullDir;
                            if (dir != null)
                            {
                                if (EditorPackageUtility.IsLinkPackage(pkg))
                                {
                                    EditorPackageUtility.DeleteLinkPackage(pkg);
                                }
                                else
                                {
                                    EditorPackageUtility.CreateLinkPackage(pkg);
                                }
                                LoadPackageList();
                                EditorPackageUtility.RefreshProject();
                            }

                        }, act =>
                        {
                            string dir = pkg.FullDir;
                            if (dir != null)
                            {
                                if ((pkg.flags & PackageFlags.LinkUsed) == PackageFlags.LinkUsed)
                                {
                                    return DropdownMenuAction.Status.Checked;
                                }
                                return DropdownMenuAction.Status.Normal;
                            }
                            return DropdownMenuAction.Status.Disabled;
                        });

                    }

                    menu.AppendAction("Copy To Packages",
                        act =>
                        {
                            string dir = pkg.FullDir;
                            if (dir != null)
                            {
                                string targetPath = Path.Combine("Packages", $"{pkg.name}@{pkg.version}");
                                bool changed = false;
                                if (Directory.Exists(targetPath))
                                {
                                    if (EditorPackageUtility.HasManifestPackage(pkg.name))
                                    {
                                        FileUtil.DeleteFileOrDirectory(targetPath);
                                        EditorPackageUtility.Log($"Delete Package '{pkg.name}', path: '{targetPath}'");
                                        changed = true;
                                    }
                                }
                                else
                                {
                                    FileUtil.CopyFileOrDirectory(dir, targetPath);
                                    EditorPackageUtility.Log($"Copy Package '{pkg.name}', path: '{targetPath}'");
                                    changed = true;
                                }
                                if (changed)
                                {
                                    EditorPackageUtility.RefreshProject();
                                }
                            }
                        },
                        act =>
                        {
                            string dir = pkg.FullDir;
                            if (dir != null)
                            {
                                string targetPath = Path.Combine("Packages", $"{pkg.name}@{pkg.version}");
                                if (Directory.Exists(targetPath))
                                {
                                    if (EditorPackageUtility.HasManifestPackage(pkg.name))
                                    {
                                        return DropdownMenuAction.Status.Checked;
                                    }
                                    else
                                    {
                                        return DropdownMenuAction.Status.Checked | DropdownMenuAction.Status.Disabled;
                                    }
                                }
                                return DropdownMenuAction.Status.Normal;
                            }
                            return DropdownMenuAction.Status.Disabled;
                        });


                    menu.AppendSeparator();

                    //e.menu.AppendAction("Copy Name", act =>
                    //{
                    //    EditorGUIUtility.systemCopyBuffer = pkg.name;
                    //}, act =>
                    //{
                    //    if (string.IsNullOrEmpty(pkg.name))
                    //        return DropdownMenuAction.Status.Disabled;
                    //    return DropdownMenuAction.Status.Normal;
                    //});


                    //e.menu.AppendAction("Copy Version", act =>
                    //{
                    //    EditorGUIUtility.systemCopyBuffer = pkg.version;
                    //}, act =>
                    //{
                    //    if (string.IsNullOrEmpty(pkg.version))
                    //        return DropdownMenuAction.Status.Disabled;
                    //    return DropdownMenuAction.Status.Normal;
                    //});

                    //e.menu.AppendSeparator();

                    menu.AppendAction("Publish", act =>
                    {
                        pkg.owner.Update();
                        //var _ = EditorPackageUtility.PublishPackage(pkg, true,
                        //    createGitTag: true);
                        var _ = EditorPackageUtility.PublishPackage(pkg.FullDir);
                        LoadPackageList();
                    }, act =>
                    {
                        if (string.IsNullOrEmpty(EditorPackageSettings.GlobalNpmAddress) && string.IsNullOrEmpty(EditorPackageSettings.NpmAddress))
                            return DropdownMenuAction.Status.Disabled;
                        if (string.IsNullOrEmpty(pkg.path))
                            return DropdownMenuAction.Status.Disabled;
                        return DropdownMenuAction.Status.Normal;
                    });

                    menu.AppendAction("Unpublish", act =>
                    {
                        pkg.owner.Update();
                        var _ = EditorPackageUtility.UnpublishPackage(EditorPackageUtility.GetNpmAuth(), pkg);
                        LoadPackageList();
                    }, act =>
                    {
                        if (string.IsNullOrEmpty(EditorPackageSettings.GlobalNpmAddress) && string.IsNullOrEmpty(EditorPackageSettings.NpmAddress))
                            return DropdownMenuAction.Status.Disabled;
                        return DropdownMenuAction.Status.Normal;
                    });
                    menu.AppendSeparator();

                    CreatePackageFolderMenu(menu, "Runtime Folder", pkg, EditorPackageUtility.PackageRuntimeFolder);
                    CreatePackageFolderMenu(menu, "Editor Folder", pkg, EditorPackageUtility.PackageEditorFolder);
                    CreatePackageFolderMenu(menu, "Documentation Folder", pkg, EditorPackageUtility.PackageDocumentationFolder);
                    CreatePackageFolderMenu(menu, "Tests Folder", pkg, EditorPackageUtility.PackageTestsFolder);
                    CreatePackageFolderMenu(menu, "Samples Folder", pkg, EditorPackageUtility.PackageSamplesFolder);

                    CreatePackageFileMenu(menu, EditorPackageUtility.PackageReadmeFile, pkg, EditorPackageUtility.PackageReadmeFile);
                    CreatePackageFileMenu(menu, EditorPackageUtility.PackageChangelogFile, pkg, EditorPackageUtility.PackageChangelogFile);
                    CreatePackageFileMenu(menu, EditorPackageUtility.PackageLicenseFile, pkg, EditorPackageUtility.PackageLicenseFile);

                    CreateOpenPackageFileMenu(menu, EditorPackageUtility.PackagePackageFile, pkg, EditorPackageUtility.PackagePackageFile);
                    CreateOpenPackageFileMenu(menu, EditorPackageUtility.PackageReadmeFile, pkg, EditorPackageUtility.PackageReadmeFile);

                    HashSet<string> defines = new HashSet<string>();
                    foreach (var define in EditorPackageUtility.GetPackageAssemblyDefines(pkg.name)
                    .SelectMany(o => o.Value))
                    {
                        defines.Add(define);
                    }

                    if (!string.IsNullOrEmpty(pkg.FullDir))
                    {
                        foreach (var define in EditorPackageUtility.GetPackageDefineConstraints(pkg.FullDir)
                        .SelectMany(o => o.Value))
                        {
                            defines.Add(define);
                        }
                    }

                    foreach (var define in defines.OrderBy(o => o))
                    {
                        if (define == "UNITY_INCLUDE_TESTS")
                            continue;

                        menu.AppendAction($"Define/{define}", act =>
                        {
                            if (EditorPackageUtility.HasDefineSymbols(define))
                            {
                                EditorPackageUtility.RemoveDefineSymbols(define);
                            }
                            else
                            {
                                EditorPackageUtility.AddDefineSymbols(define);
                            }
                        },
                        act =>
                        {
                            if (EditorPackageUtility.HasDefineSymbols(define))
                                return DropdownMenuAction.Status.Checked;
                            return DropdownMenuAction.Status.Normal;
                        });
                    }

                    if (!string.IsNullOrEmpty(pkg.path))
                    {
                        menu.AppendAction("Fix Compile Error", act =>
                        {
                            EditorPackageUtility.FixPacakgeCompileError(pkg.name);
                        });


                        menu.AppendAction("Refresh", act =>
                        {
                            EditorPackageUtility.UpdatePackage(pkg);
                            pkg.owner.Save();
                            LoadPackageList();
                        });

                        menu.AppendAction("Show In Explorer", act =>
                        {
                            string path = pkg.FilePath;
                            if (!string.IsNullOrEmpty(path))
                            {
                                if (File.Exists(path))
                                {
                                    EditorUtility.RevealInFinder(path);
                                }
                            }

                        });
                    }

                }));

                return container;
            };

            packageListView.bindItem = (view, index) =>
            {
                var item = packageListView.itemsSource[index] as PackageInfo;
                view.userData = item;

                PackageData data = GetPackageData(item);


                var check = view.Q<Toggle>(className: "package-item_check");

                check.SetValueWithoutNotify(item.IsFavorite && item.IsUsed);

                var starLabel = view.Q<Label>(className: "package-item_star");
                starLabel.RemoveFromClassList("package-item_star-active");
                if (item.IsFavorite)
                {
                    starLabel.AddToClassList("package-item_star-active");
                }


                var nameLabel = view.Q<Label>(className: "package-item_name");
                nameLabel.text = string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
                if ((item.flags & PackageFlags.Missing) != 0)
                {
                    view.AddToClassList("package-item-missing");
                }
                else
                {
                    view.RemoveFromClassList("package-item-missing");
                }

                if (data.changed)
                {
                    nameLabel.text += " *";
                }

                var pathLabel = view.Q<Label>(className: "package-item_path");
                view.RemoveFromClassList("package-link");
                view.RemoveFromClassList("package-local");

                if (!string.IsNullOrEmpty(item.owner.localDir))
                {
                    pathLabel.style.display = DisplayStyle.Flex;

                    if ((item.flags & PackageFlags.PackageCache) == PackageFlags.PackageCache)
                    {
                        pathLabel.text = "[PackageCache]";
                    }
                    else if (SelectedRepository == EditorPackageUtility.StarRepsitory)
                    {
                        pathLabel.text = $"[{item.owner.name}] {item.path}";
                    }
                    else
                    {
                        pathLabel.text = item.path;
                    }

                    if (EditorPackageUtility.HasManifestPackage(item.name, item.GetManifestUri()))
                    {
                        view.AddToClassList("package-local");
                    }

                    if (EditorPackageUtility.IsLinkPackageByPath(item.FullDir))
                    {
                        view.AddToClassList("package-link");
                    }
                }
                else
                {
                    pathLabel.style.display = DisplayStyle.None;
                }

                var codeLineLabel = view.Q<Label>(className: "package-item_code-line");
                var versionLabel = view.Q<Label>(className: "package-item_version");
                if (item.totalCodeLine > 0)
                {
                    codeLineLabel.text = $"{item.totalCodeFile}/" + EditorPackageUtility.LengthToUnitString(item.totalCodeLine);
                }
                else
                {
                    codeLineLabel.text = null;
                }


                versionLabel.text = item.version;
                versionLabel.RemoveFromClassList("package-version_upgrade");
                versionLabel.RemoveFromClassList("package-version_degrade");
                //if (EditorPackageUtility.HasManifestPackage(item.name, item.version))
                //{
                //    versionLabel.AddToClassList("package-version");
                //}

                string manifestVersionStr = EditorPackageUtility.GetManifestVersion(item.name);
                if (!string.IsNullOrEmpty(manifestVersionStr) && Version.TryParse(manifestVersionStr, out var manifestVersion))
                {
                    string color = "Yellow";
                    if (item.Version.CompareTo(manifestVersion) > 0)
                    {
                        versionLabel.text += $"↑";

                        if (!versionLabel.ClassListContains("package-version_upgrade"))
                        {
                            versionLabel.AddToClassList("package-version_upgrade");
                        }
                    }
                    else if (item.Version.CompareTo(manifestVersion) < 0)
                    {
                        versionLabel.text += "↓";
                        versionLabel.AddToClassList("package-version_degrade");
                    }
                }

                view.tooltip = item.path;
            };

            packageListView.selectionChanged += (selected) =>
            {
                var item = selected.FirstOrDefault() as PackageInfo;
                //EditorApplication.delayCall += () =>
                {
                    if (item != null)
                    {
                        item.OpenInspector();
                    }
                    //LoadPackageList();
                };
            };

            LoadRepositoryList();
            repositoryListView.selectedIndex = 0;

            Version v;
            //PackageVersion.TryParse("1", out  v);
            //PackageVersion.TryParse("1.2", out v);
            //PackageVersion.TryParse("1.2.3", out v);
            //PackageVersion.TryParse("1.2.3.4", out v);
            //PackageVersion.TryParse("1.2.3.4.5", out v);
            //PackageVersion.TryParse("1.2.3-preview-10", out v);
            //PackageVersion.TryParse("1.2.3-pre-10", out v);
            //PackageVersion.TryParse("1.2.3-pre10", out v);
            //PackageVersion.TryParse("1.2.3-pre.10", out v); 
            //PackageVersion.TryParse("0.0.5-preview-5", out var v1);
            //PackageVersion.TryParse("0.0.5-preview-5", out var v2);
            //v1.CompareTo(v2);
        }


        void RefreshRepository(PackageRepository repository, bool update = true)
        {
            if (repository == EditorPackageUtility.StarRepsitory)
                return;
            EditorUtility.DisplayProgressBar("Scan Local Packages", repository.url, 0f);
            if (repository.reference != null)
            {
                repository.reference.ScanLocalPackages();

            }
            else
            {
                repository.ScanLocalPackages();
            }

            if (update)
            {
                foreach (var pkg in repository.GetPackages())
                {
                    EditorUtility.DisplayProgressBar("Update Packages", pkg.name, 0f);
                    EditorPackageUtility.UpdatePackage(pkg);
                }
            }

            repository.Save();
            EditorUtility.ClearProgressBar();
        }

        void CreatePackageFolderMenu(DropdownMenu menu, string menuName, PackageInfo packageInfo, string folder)
        {
            menu.AppendAction($"Create/{menuName}", act =>
            {
                if (string.IsNullOrEmpty(packageInfo.FullDir))
                    return;
                string path = Path.Combine(packageInfo.FullDir, folder);
                Directory.CreateDirectory(path);
                EditorUtility.RevealInFinder(path);
            }, act =>
            {
                if (string.IsNullOrEmpty(packageInfo.FullDir))
                    return DropdownMenuAction.Status.Disabled;
                string path = Path.Combine(packageInfo.FullDir, folder);
                if (Directory.Exists(path))
                    return DropdownMenuAction.Status.Disabled;
                return DropdownMenuAction.Status.Normal;
            });
        }

        void CreatePackageFileMenu(DropdownMenu menu, string menuName, PackageInfo packageInfo, string file)
        {
            menu.AppendAction($"Create/{menuName}", act =>
            {
                if (string.IsNullOrEmpty(packageInfo.FullDir))
                    return;
                string path = Path.Combine(packageInfo.FullDir, file);
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, string.Empty, Encoding.UTF8);
                }

            }, act =>
            {
                if (string.IsNullOrEmpty(packageInfo.FullDir))
                    return DropdownMenuAction.Status.Disabled;
                string path = Path.Combine(packageInfo.FullDir, file);
                if (File.Exists(path))
                    return DropdownMenuAction.Status.Disabled;
                return DropdownMenuAction.Status.Normal;
            });
        }

        void CreateOpenPackageFileMenu(DropdownMenu menu, string menuName, PackageInfo packageInfo, string file)
        {
            menu.AppendAction($"Open/{menuName}", act =>
            {
                if (string.IsNullOrEmpty(packageInfo.FullDir))
                    return;
                string path = Path.Combine(packageInfo.FullDir, file);
                if (!File.Exists(path))
                {
                    return;
                }

                Application.OpenURL(path);
            }, act =>
            {
                if (string.IsNullOrEmpty(packageInfo.FullDir))
                    return DropdownMenuAction.Status.Disabled;
                string path = Path.Combine(packageInfo.FullDir, file);
                if (!File.Exists(path))
                    return DropdownMenuAction.Status.Disabled;
                return DropdownMenuAction.Status.Normal;
            });
        }

        List<PackageRepository> repositories;

        void LoadRepositoryList()
        {
            repositories = EditorPackageSettings.LoadRepositories();
            repositoryListView.itemsSource = new PackageRepository[] { EditorPackageUtility.StarRepsitory }.Concat(repositories
                .OrderByDescending(o => o == EditorPackageUtility.ProjectRepsitory)
                .ThenBy(o => o.name))
                .ToList();
            repositoryListView.RefreshItems();
            LoadPackageList();
        }

        IEnumerable<PackageInfo> GetMissingFavorites(PackageRepository repository)
        {

            List<PackageInfo> missingList = new List<PackageInfo>();
            foreach (var item in repository.GetMissingFavorites())
            {
                missingList.Add(new PackageInfo()
                {
                    name = item.name,
                    displayName = item.displayName,
                    path = item.path,
                    owner = repository.reference ?? repository,
                    flags = PackageFlags.Missing | PackageFlags.Favorite,
                });
            }
            return missingList;
        }

        void LoadPackageList()
        {
            EditorPackageUtility.Refresh();
            PackageRepository repository = SelectedRepository;
            packageListView.itemsSource = null;

            IEnumerable<PackageInfo> packages = null;

            packageDatas.Clear();

            if (repository != null)
            {
                if (repository == EditorPackageUtility.StarRepsitory)
                {
                    EditorPackageUtility.StarRepsitory.packages.Clear();
                    foreach (var repo in repositories)
                    {
                        repo.Update();
                        foreach (var pkg in repo.GetPackages().Concat(GetMissingFavorites(repo)))
                        {
                            if (pkg.IsFavorite)
                            {
                                EditorPackageUtility.StarRepsitory.packages.Add(pkg);
                            }
                        }
                    }
                    packages = repository.GetPackages();
                }
                else
                {
                    //if (repository == EditorPackageUtility.ProjectRepsitory)
                    //{
                    //    if (repository.packages.Count == 0)
                    //    {
                    //        RefreshRepository(repository);
                    //    }
                    //}

                    repository.Update();
                    packages = repository.GetPackages();
                    packages = GetMissingFavorites(repository).Concat(packages).ToList();
                }


                if (!string.IsNullOrEmpty(searchText))
                {
                    Regex regex = new Regex(searchText, RegexOptions.IgnoreCase);

                    packages = packages.Where(o => regex.IsMatch(o.name) ||
                    (o.displayName != null && regex.IsMatch(o.displayName)) ||
                    (o.description != null && regex.IsMatch(o.description)) ||
                    (o.category != null && regex.IsMatch(o.category)) ||
                    (o.keywords != null && o.keywords.Any(o => regex.IsMatch(o))) ||
                    (o.path != null && regex.IsMatch(o.path)));

                }

                packages = from o in packages
                           orderby (o.IsFavorite && o.IsUsed) descending,
                           o.IsFavorite descending,
                           o.displayName ?? o.name,
                           o.version descending
                           select o;
                packageListView.itemsSource = packages.ToList();

            }


            packageListView.RefreshItems();
            packageListView.ScrollToItem(0);

        }

        PackageData GetPackageData(PackageInfo packageInfo)
        {
            if (packageDatas.TryGetValue(packageInfo, out var data))
                return data;
            data = new PackageData();

            if (!string.IsNullOrEmpty(packageInfo.FullDir))
            {
                if (EditorPackageUtility.IsGitRootDir(packageInfo.FullDir))
                {
                    using (var repo = new GitRepository(packageInfo.FullDir))
                    {
                        data.changed = repo.HasChanged();
                    }
                }
            }
            packageDatas[packageInfo] = data;
            return data;
        }

    }

}