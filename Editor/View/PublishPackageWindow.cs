using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.SettingsManagement.Editor;
using Unity.SettingsManagement;
using System;
using System.Threading.Tasks;
using System.IO;
namespace Unity.PackageManagement
{
    class PublishPackageWindow : EditorWindow
    {
        private VisualElement root;
        private string packageDir;
        public PackageInfo packageInfo;

        Label nameField;
        Label displayNameField;
        Label locationField;
        Label versionField;
        DropdownField registryMenu;
        DropdownField incrementMenu;
        DropdownField preIdMenu;
        DropdownField preSeparatorMenu;

        TextField newVersionField;
        Toggle createTagField;
        Toggle pushField;
        Toggle forceField;
        private Label errorField;
        Button publishButton;
        private string newVersion;
        private string generatedVersion;
        private bool createTag = true;
        private bool isPush = true;
        private bool isDone;
        public bool success = false;
        public bool isForce = false;


        Dictionary<int, string> incrementFieldNames = new()
        {
           { Version.MAJOR,"Major" },
           { Version.MINOR,"Minor" },
           { Version.BUILD,"Build" },
           { Version.REVISION,"Revision" }
        };

        public bool IsDone => isDone;


        private void OnEnable()
        {

            CreateUI();
        }

        private void OnDisable()
        {
            isDone = true;
        }

        public static PublishPackageWindow OpenWinow(string packageDir)
        {
            var win = CreateInstance<PublishPackageWindow>();
            win.titleContent = new GUIContent("Publish Package");
            win.packageDir = packageDir;

            if (EditorPackageUtility.IsGitRootDir(packageDir))
            {
                win.createTag = true;
                win.isPush = true;
            }
            else
            {
                win.createTag = false;
                win.isPush = false;
            }

            win.ShowModal();
            return win;
        }

        public void CreateUI()
        {
            root = EditorSettingsUtility.LoadUXML(EditorSettingsUtility.GetEditorUXMLPath(SettingsUtility.GetPackageName(GetType()), nameof(PublishPackageWindow)));
            root.style.flexGrow = 1f;
            nameField = root.Q("name").Q<Label>(className: "unity-base-field__input");
            displayNameField = root.Q("display-name").Q<Label>(className: "unity-base-field__input");
            versionField = root.Q("version").Q<Label>(className: "unity-base-field__input");
            locationField = root.Q("location").Q<Label>(className: "unity-base-field__input");
            registryMenu = root.Q<DropdownField>("registry");
            incrementMenu = root.Q<DropdownField>("increment");
            preIdMenu = root.Q<DropdownField>("pre-id");
            preSeparatorMenu = root.Q<DropdownField>("pre-separator");
            newVersionField = root.Q<TextField>("new-version");
            createTagField = root.Q<Toggle>("create-tag");
            pushField = root.Q<Toggle>("push");
            forceField = root.Q<Toggle>("force");
            errorField = root.Q<Label>("error");
            publishButton = root.Q<Button>("publish");
            var openFolderButton = root.Q<Button>("open-dir");

            registryMenu.RegisterValueChangedCallback(e =>
            {
            });

            incrementMenu.RegisterValueChangedCallback(e =>
            {
                GenerateVersion();
            });

            preIdMenu.RegisterValueChangedCallback(e =>
            {
                GenerateVersion();
            });

            preSeparatorMenu.RegisterValueChangedCallback(e =>
            {
                GenerateVersion();
            });


            newVersionField.RegisterValueChangedCallback(e =>
            {
                newVersion = e.newValue;
                OnNewVersionChanged();
            });

            createTagField.RegisterValueChangedCallback(e =>
            {
                createTag = e.newValue;
            });

            pushField.RegisterValueChangedCallback(e =>
            {
                isPush = e.newValue;
            });
            forceField.RegisterValueChangedCallback(e =>
            {
                isForce = e.newValue;
            });

            publishButton.clicked += async () =>
            {
                try
                {
                    await Publish();
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("Publish Package Error", ex.Message, "ok");
                }
            };

            openFolderButton.clicked += () =>
            {
                if (!string.IsNullOrEmpty(packageDir))
                {
                    var path = Path.GetFullPath(packageDir);
                    if (Directory.Exists(path))
                    {
                        EditorUtility.RevealInFinder(path);
                    }
                }
            };

        }

        public void CreateGUI()
        {
            rootVisualElement.Add(root);
            Refresh();
        }
        Dictionary<string, NpmAuth> auths = new Dictionary<string, NpmAuth>();

        void Refresh()
        {
            Clear();

            if (!string.IsNullOrEmpty(packageDir))
            {
                locationField.text = Path.GetFullPath(packageDir).Replace("\\", "/");
                locationField.tooltip = locationField.text;
                packageInfo = PackageInfo.TryParse(packageDir);
            }

            if (packageInfo == null)
            {
                root.SetEnabled(false);
                return;
            }

            var version = packageInfo.Version;

            root.SetEnabled(true);

            nameField.text = packageInfo.name;
            displayNameField.text = packageInfo.displayName;
            versionField.text = version;

            if (!string.IsNullOrEmpty(EditorPackageSettings.NpmAddress))
            {
                string repo = $"{EditorPackageSettings.NpmAddress}:{EditorPackageSettings.NpmPort} (npm)";
                registryMenu.choices.Add(repo);
                auths[repo] = EditorPackageUtility.GetNpmAuth(EditorPackageSettings.NpmAddress, EditorPackageSettings.NpmPort);
            }

            if (!string.IsNullOrEmpty(EditorPackageSettings.GlobalNpmAddress))
            {
                string repo = $"{EditorPackageSettings.GlobalNpmAddress}:{EditorPackageSettings.GlobalNpmPort} (npm)";
                registryMenu.choices.Add(repo);
                auths[repo] = EditorPackageUtility.GetNpmAuth(EditorPackageSettings.GlobalNpmAddress, EditorPackageSettings.GlobalNpmPort);
            }

            if (registryMenu.choices.Count > 0)
            {
                registryMenu.SetValueWithoutNotify(registryMenu.choices[0]);
            }


            incrementMenu.choices.Add("None");

            for (int i = 0; i < 4; i++)
            {
                incrementMenu.choices.Add(incrementFieldNames[i]);
            }
            incrementMenu.choices.Add($"Pre");
            for (int i = 0; i < 4; i++)
            {
                incrementMenu.choices.Add($"Pre {incrementFieldNames[i]}");
            }

            if (version.hasPre)
            {
                incrementMenu.SetValueWithoutNotify("Pre");
            }
            else
            {
                if (version.FieldCount > 0)
                {
                    incrementMenu.SetValueWithoutNotify(incrementFieldNames[version.FieldCount - 1]);
                }
            }

            preIdMenu.choices.Add("pre");
            preIdMenu.choices.Add("preview");
            preIdMenu.choices.Add("alpha");
            preIdMenu.choices.Add("release");

            if (version.hasPre && !preIdMenu.choices.Contains(version.PreId))
            {
                preIdMenu.choices.Add(version.PreId);
            }

            preIdMenu.choices.Sort();

            if (version.hasPre)
            {
                preIdMenu.SetValueWithoutNotify(version.PreId);
            }
            else
            {
                preIdMenu.SetValueWithoutNotify("preview");
            }

            preSeparatorMenu.choices.Add("-");
            preSeparatorMenu.choices.Add(".");

            if (version.hasPre && !preSeparatorMenu.choices.Contains(version.PreSeparator))
            {
                preSeparatorMenu.choices.Add(version.PreSeparator);
            }

            if (version.hasPre)
            {
                preSeparatorMenu.SetValueWithoutNotify(version.PreSeparator);
            }
            else
            {
                preSeparatorMenu.SetValueWithoutNotify("-");
            }
            GenerateVersion();

            createTagField.SetValueWithoutNotify(createTag);
            pushField.SetValueWithoutNotify(isPush);

            OnNewVersionChanged();
        }

        void Clear()
        {
            nameField.text = null;
            displayNameField.text = null;
            versionField.text = null;
            locationField.text = null;
            locationField.tooltip = null;
            registryMenu.choices.Clear();
            incrementMenu.choices.Clear();
            preIdMenu.choices.Clear();
            preSeparatorMenu.choices.Clear();
            newVersionField.SetValueWithoutNotify(null);
            createTagField.SetValueWithoutNotify(false);
            pushField.SetValueWithoutNotify(false);
            forceField.SetValueWithoutNotify(false);
            errorField.text = null;
        }


        private void GenerateVersion()
        {
            generatedVersion = null;
            OnNewVersionChanged();
            SetError(null);


            Version oldVersion = packageInfo?.Version;
            if (oldVersion == null)
                return;

            if (incrementMenu.value == "None")
            {
                generatedVersion = oldVersion.ToString();
                OnNewVersionChanged();
                return;
            }

            Version newVersion = new Version();

            bool isPre = false;
            string fieldName = null;
            int field = -1;
            string preId, preSeparator;

            if (incrementMenu.value == "Pre")
            {
                isPre = true;
            }
            else if (incrementMenu.value.StartsWith("Pre "))
            {
                isPre = true;
                fieldName = incrementMenu.value.Substring("Pre ".Length);
            }
            else
            {
                fieldName = incrementMenu.value;
            }


            if (!string.IsNullOrEmpty(fieldName))
            {
                foreach (var item in incrementFieldNames)
                {
                    if (item.Value == fieldName)
                    {
                        field = item.Key;
                    }
                }
            }

            preId = preIdMenu.value;
            preSeparator = preSeparatorMenu.value;

            if (field >= 0 && field >= oldVersion.FieldCount)
            {
                SetError("Increment overflow Field Count");
                return;
            }

            if (isPre)
            {
                if (string.IsNullOrEmpty(preId))
                {
                    SetError("Pre Id empty");
                    return;
                }

                if (string.IsNullOrEmpty(preSeparator))
                {
                    SetError("Pre Separator empty");
                    return;
                }


                newVersion = oldVersion.PreIncrement(field, preId, preSeparator);
            }
            else
            {
                newVersion = oldVersion.Increment(field);
            }

            generatedVersion = newVersion.ToString();
            OnNewVersionChanged();
        }

        void SetError(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                errorField.text = null;
                publishButton.SetEnabled(true);
                return;
            }

            errorField.text = error;
            publishButton.SetEnabled(false);
        }

        void OnNewVersionChanged()
        {
            if (!string.IsNullOrEmpty(this.newVersion))
            {
                newVersionField.SetValueWithoutNotify(newVersion);
                newVersionField.labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                newVersionField.SetValueWithoutNotify(generatedVersion);
                newVersionField.labelElement.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }


        async Task Publish()
        {
            if (packageInfo == null)
                throw new Exception("Package Info null");

            NpmAuth auth;
            string registry = registryMenu.value;
            if (string.IsNullOrEmpty(registry))
                throw new Exception("Registry null");
            auths.TryGetValue(registry, out auth);

            if (auth == null)
                throw new Exception("Registry null");

            string oldVersion = packageInfo.Version.ToString();

            string newVersion;
            if (!string.IsNullOrEmpty(this.newVersion))
            {
                newVersion = this.newVersion;
            }
            else
            {
                newVersion = generatedVersion;
            }
            if (string.IsNullOrEmpty(newVersion))
                throw new Exception("New Version null");

            newVersion = await EditorPackageUtility.PublishPackage(auth, packageDir, newVersion, createTag: createTag, push: isPush, force: isForce);
            packageInfo.Version = newVersion;


            success = true;
            if (oldVersion != newVersion)
            {
                EditorUtility.DisplayDialog("Publish Pacakge Success", $"[{packageInfo.name}] v{oldVersion} => v{newVersion}", "ok");
            }
            else
            {
                EditorUtility.DisplayDialog("Publish Pacakge Success", $"[{packageInfo.name}] v{newVersion}", "ok");
            }
            Close();
        }


    }
}