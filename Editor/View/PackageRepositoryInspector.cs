using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Unity.SettingsManagement.Editor;

namespace Unity.PackageManagement
{
    [CustomEditor(typeof(PackageRepository.InspectorObject))]
    class PackageRepositoryInspector : UnityEditor.Editor
    {
        PackageRepository Target => (target as PackageRepository.InspectorObject).target;

        private void OnEnable()
        {

        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();

            VisualElement fieldContainer = new VisualElement();
            root.Add(fieldContainer);
            if (!Target.editable)
            {
                fieldContainer.SetEnabled(false);
            }

            var p = serializedObject.FindProperty("target");
            bool isField = true;

            if (p.Next(true))
            {
                int depth = p.depth;
                do
                {
                    if (p.depth < depth)
                        break;
                    if (p.name == nameof(PackageRepository.packages))
                        continue;
                    if (p.name == nameof(PackageRepository.favorites))
                        continue;

                    PropertyField propertyField = new PropertyField(p.Copy());

                    propertyField.RegisterValueChangeCallback(e =>
                    {
                        if (isField)
                            return;
                        var target = Target;
                        target.Update();
                        if (target.IsLocal && target.reference == null)
                        {
                            if (target.reference == null)
                            {
                                target.reference = PackageRepository.LoadLocal(target.localDir);
                                target.Update();
                            }
                            else if (target.reference.localDir != target.localDir)
                            {
                                target.reference = PackageRepository.LoadLocal(target.localDir);
                                target.Update();
                            }
                        }

                        if (target.reference != null)
                        {
                            target.reference.name = target.name;
                            target.reference.excludeNames = target.excludeNames;
                            target.reference.excludePaths = target.excludePaths;
                            if (target.IsLocal)
                            {
                                target.reference.localDir = target.localDir;
                            }
                        }

                        if (target.IsLocal)
                        {
                            target.Save();
                        }
                        EditorPackageSettings.Save();

                    });

                    if (Target == EditorPackageUtility.ProjectRepsitory)
                    {
                        switch (p.name)
                        {
                            case nameof(PackageRepository.name):
                            case nameof(PackageRepository.url):
                                propertyField.SetEnabled(false);
                                break;
                        }
                    }

                    fieldContainer.Add(propertyField);
                } while (p.Next(false));

                EditorApplication.delayCall += () =>
                {
                    isField = false;
                };

            }

            if (Target == EditorPackageUtility.StarRepsitory)
            {
                VisualElement settingsContainer = new VisualElement();
                root.Add(settingsContainer);

                EditorSettingsUtility.CreateSettingView(settingsContainer, typeof(EditorPackageSettings));
            }

            return root;
        }


    }
}