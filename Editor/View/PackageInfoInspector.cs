using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using Unity.SettingsManagement.Editor;

namespace Unity.PackageManagement
{
    [CustomEditor(typeof(PackageInfo.InspectorObject))]
    class PackageInfoInspector : UnityEditor.Editor
    {
        VisualElement root;
        //Label repoLabel;
        Label pathLabel;

        new PackageInfo target => (base.target as PackageInfo.InspectorObject).target;


        private void OnEnable()
        {

        }
        private void OnDisable()
        {
            //Debug.Log("OnDisable " + target?.name + ", " + (target == null));
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (target == null)
                return null;
            root = new VisualElement();

            //repoLabel = new Label();
            //repoLabel.AddToClassList("package_repo");

            //root.Add(repoLabel);

            string repoName = null;
            if (target.owner != null)
            {
                repoName = target.owner.name;
            }

            pathLabel = new Label();
            pathLabel.AddToClassList("package_path");
            if (string.IsNullOrEmpty(target.path))
            {
                pathLabel.style.display = DisplayStyle.None;
            }
            else
            {
                pathLabel.style.display = DisplayStyle.Flex;
                pathLabel.text = $"[{repoName}] {target.path}";
                pathLabel.tooltip = target.path;
            }
            root.Add(pathLabel);


            root.Add(CreateLabelField("CodeLine", target.totalCodeLine.ToString()));

            var p = serializedObject.FindProperty("target");
            if (p.Next(true))
            {
                int depth = p.depth;
                do
                {
                    if (p.depth < depth)
                        break;

                    if (p.name == nameof(PackageInfo.path))
                        continue;

                    PropertyField propertyField = new PropertyField(p.Copy());
                    propertyField.RegisterValueChangeCallback(e =>
                    {

                    });
                    root.Add(propertyField);
                } while (p.Next(false));
            }


            UpdateView();

            return root;
        }

        VisualElement CreateLabelField(string label, string value)
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("unity-base-field");

            Label nameLabel = new Label();
            nameLabel.AddToClassList("unity-base-field__label");
            nameLabel.text = label;
            container.Add(nameLabel);

            Label valueLabel = new Label();
            valueLabel.text = value;
            container.Add(valueLabel);
            return container;
        }

        void UpdateView()
        {

            string filePath = target.FilePath;
            if (!(!string.IsNullOrEmpty(filePath) && File.Exists(filePath)))
            {
                root.SetEnabled(false);
            }

            //if (target.owner != null)
            //{
            //    repoLabel.text = target.owner.name;
            //}
        }

 
    }
}