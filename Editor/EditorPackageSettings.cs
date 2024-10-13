using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Unity.SettingsManagement;
using Unity.SettingsManagement.Editor;
using SettingsScope = Unity.SettingsManagement.SettingsScope;

namespace Unity.PackageManagement
{
    [Serializable]
    public class EditorPackageSettings
    {
        private static Settings settings;

        private static Settings Settings
            => settings ??= new Settings(new EditorSettingsRepository(EditorPackageUtility.UnityPackageName),
                new PackageSettingRepository(EditorPackageUtility.UnityPackageName, SettingsScope.EditorProject));

        [HideInInspector]
        private static Setting<List<PackageRepository>> repositories = new(Settings, "repositories", new(), SettingsManagement.SettingsScope.UnityEditor);

        public static List<PackageRepository> Repositories
        {
            get => repositories.Value;
            set => repositories.SetValue(value, true);
        }

        private static Setting<string> npmAddress = new(Settings, nameof(NpmAddress), null, SettingsManagement.SettingsScope.EditorProject);

        public static string NpmAddress
        {
            get => npmAddress.Value;
            set => npmAddress.SetValue(value.Trim(), true);
        }


        private static Setting<ushort> npmPort = new(Settings, nameof(NpmPort), 0, SettingsManagement.SettingsScope.EditorProject);

        public static ushort NpmPort
        {
            get => npmPort.Value;
            set => npmPort.SetValue(value, true);
        }

        private static Setting<string> npmAuthToken = new(Settings, nameof(NpmAuthToken), null, SettingsManagement.SettingsScope.EditorProject);

        public static string NpmAuthToken
        {
            get => npmAuthToken.Value;
            set => npmAuthToken.SetValue(value, true);
        }


        private static Setting<string> globalNpmAddress = new(Settings, nameof(NpmAddress), null, SettingsManagement.SettingsScope.UnityEditor);

        public static string GlobalNpmAddress
        {
            get => globalNpmAddress.Value;
            set => globalNpmAddress.SetValue(value.Trim(), true);
        }


        private static Setting<ushort> globalNpmPort = new(Settings, nameof(NpmPort), 0, SettingsManagement.SettingsScope.UnityEditor);

        public static ushort GlobalNpmPort
        {
            get => globalNpmPort.Value;
            set => globalNpmPort.SetValue(value, true);
        }

        private static Setting<string> globalNpmAuthToken = new(Settings, nameof(NpmAuthToken), null, SettingsManagement.SettingsScope.UnityEditor);

        public static string GlobalNpmAuthToken
        {
            get => globalNpmAuthToken.Value;
            set => globalNpmAuthToken.SetValue(value, true);
        }



        public static List<PackageRepository> LoadRepositories()
        {
            List<PackageRepository> repos = new();

            PackageRepository projectRepo = EditorPackageUtility.ProjectRepsitory;
            repos.Add(projectRepo);

            foreach (var repo in repositories.Value)
            {
                if (repo.IsLocal)
                {
                    PackageRepository localRepo = PackageRepository.LoadLocal(repo.localDir);
                    repo.reference = localRepo;
                    repo.name = localRepo.name;
                    repo.excludeNames = localRepo.excludeNames;
                    repo.excludePaths = localRepo.excludePaths;
                }
                repo.Update();
                repos.Add(repo);
            }
            return repos;
        }



        public static void Save()
        {

            Settings.Save();
        }

    }
}