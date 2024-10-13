using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.PackageManagement
{

    [Serializable]
    public class FavoritePackage : IEquatable<PackageInfo>
    {
        public string name;
        public string displayName;
        public string path;
        public string version;

        public bool Equals(PackageInfo package)
        {
            if (name != package.name)
                return false;

            if (!string.IsNullOrEmpty(path))
            {
                if (path != package.path)
                    return false;
            }
            else
            {
                if (!string.IsNullOrEmpty(version))
                {
                    if (version != package.version)
                        return false;
                }
            }

            return true;
        }
    }
}