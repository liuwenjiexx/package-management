using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Unity
{

    /// <summary>
    /// 样例：1.0.0, 1.0.0-pre-0, 1.0.0-pre.0, 1.0.0-preview-0, 1.0.0-release-0, 1.0.0-alpha.0
    /// </summary>
    [Serializable]
    public class Version : IEquatable<Version>, IComparable<Version>
    {
        [SerializeField]
        public int major;
        [SerializeField]
        public int minor;
        [SerializeField]
        public int build;
        [SerializeField]
        public int revision;
        [SerializeField]
        public string preId;
        [SerializeField]
        public string preSeparator;
        /// <summary>
        /// 预发布版本号, pre-release
        /// </summary>
        [SerializeField]
        public int pre;
        [SerializeField]
        private int fieldCount;
        [SerializeField]
        public bool hasPre;

        public static readonly Version Empty = new Version();

        public const string SEPARATOR = ".";
        const string PRE_PREFIX = "-";


        public const int NONE = -1;
        public const int MAJOR = 0;
        public const int MINOR = 1;
        public const int BUILD = 2;
        public const int REVISION = 3;

        private static Regex versionRegex;

        public Version() { }


        public Version(int major)
            : this(major, 0, 0, 0, 1)
        {
        }

        public Version(int major, int minor)
            : this(major, minor, 0, 0, 2)
        {
        }

        public Version(int major, int minor, int build)
            : this(major, minor, build, 0, 3)
        {
        }

        public Version(int major, int minor, int build, int revision)
            : this(major, minor, build, revision, 4)
        {
        }

        private Version(int major, int minor, int build, int revision, int fieldCount)
        {
            this.major = major;
            this.minor = minor;
            this.build = build;
            this.revision = revision;
            this.fieldCount = fieldCount;
        }

        private Version(int major, int minor, int build, int revision, int fieldCount, bool hasPre, string preId, string preSeparator, int pre)
        {
            this.major = major;
            this.minor = minor;
            this.build = build;
            this.revision = revision;
            this.preId = preId;
            this.preSeparator = preSeparator;
            this.pre = pre;
            this.fieldCount = fieldCount;
            this.hasPre = hasPre;
        }

        public Version(Version other)
        {
            this.major = other.major;
            this.minor = other.minor;
            this.build = other.build;
            this.revision = other.revision;
            this.preId = other.preId;
            this.preSeparator = other.preSeparator;
            this.pre = other.pre;
            this.fieldCount = other.fieldCount;
            this.hasPre = other.hasPre;
        }

        public int this[int index]
        {
            get
            {
                if (index >= fieldCount)
                    throw new IndexOutOfRangeException();

                switch (index)
                {
                    case MAJOR:
                        return major;
                    case MINOR:
                        return minor;
                    case BUILD:
                        return build;
                    case REVISION:
                        return revision;
                }
                return revision;
            }

            private set
            {
                if (index >= fieldCount)
                    throw new IndexOutOfRangeException();

                switch (index)
                {
                    case MAJOR:
                        major = value;
                        break;
                    case MINOR:
                        minor = value;
                        break;
                    case BUILD:
                        build = value;
                        break;
                    case REVISION:
                        revision = value;
                        break;
                }
            }
        }

        private static Regex VersionRegex
        {
            get
            {
                if (versionRegex == null)
                {
                    versionRegex = new Regex("^(?<major>\\d+)(\\.(?<minor>\\d+))*(-(?<pre_id>\\S+?)(?<pre_sep>[-\\.])?(?<pre>\\d+))?$", RegexOptions.Compiled);
                }
                return versionRegex;
            }
        }

        public int Major => major;

        public int Minor => minor;

        public int Build => build;

        public int Revision => revision;

        /// <summary>
        /// 预发布版本号Id, 参考值 [pre, preview, alpha] 或自定义名称, 如: 1.0-pre-0, 1.0-preview-0
        /// </summary>
        public string PreId => preId;

        /// <summary>
        /// 预发布版本号分隔符，参考值 [. -], 如: 1.0-pre-0, 1.0-pre.0
        /// </summary>
        public string PreSeparator => preSeparator;

        /// <summary>
        /// 预发布版本号, 如: 1.0-pre-0, 1.0-pre-1
        /// </summary>
        [SerializeField]
        public int Pre => pre;


        public int FieldCount => fieldCount;


        public static bool TryParse(string version, out Version result)
        {
            try
            {
                result = Parse(version);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        public static Version Parse(string version)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            version = version.Trim();
            if (string.IsNullOrEmpty(version))
                throw new ArgumentNullException(nameof(version));
            Version ver = new Version();
            var m = VersionRegex.Match(version);
            if (!m.Success)
                throw new FormatException();
            ver.major = int.Parse(m.Groups["major"].Value);
            ver.fieldCount = 1;
            var minorGroup = m.Groups["minor"];
            if (minorGroup.Success)
            {
                foreach (Capture c in minorGroup.Captures)
                {
                    if (!int.TryParse(c.Value, out var n))
                        throw new FormatException($"[{c.Value}] Not Integer");
                    switch (ver.fieldCount)
                    {
                        case 1:
                            ver.minor = n;
                            ver.fieldCount++;
                            break;
                        case 2:
                            ver.build = n;
                            ver.fieldCount++;
                            break;
                        case 3:
                            ver.revision = n;
                            ver.fieldCount++;
                            break;
                        default:
                            throw new FormatException($"Version '{version}' overflow index: " + ver.fieldCount);
                    }
                }
            }

            var preGroup = m.Groups["pre"];
            if (preGroup.Success)
            {
                if (!int.TryParse(preGroup.Value, out var pre))
                    throw new FormatException($"[{preGroup.Value}] Not Integer");
                ver.pre = pre;
                ver.preId = m.Groups["pre_id"].Value;
                ver.preSeparator = m.Groups["pre_sep"].Value;
                ver.hasPre = true;
            }

            return ver;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj is Version ver)
            {
                return Equals(ver);
            }
            return base.Equals(obj);
        }

        public bool Equals(Version other)
        {
            if (other == null)
            {
                if (fieldCount == 0) return true;
                return false;
            }

            if (fieldCount != other.fieldCount)
                return false;
            if (major != other.major) return false;
            if (fieldCount > 1 && minor != other.minor) return false;
            if (fieldCount > 2 && build != other.build) return false;
            if (fieldCount > 3 && revision != other.revision) return false;

            if (hasPre || other.hasPre)
            {
                if (preId != other.preId) return false;
                if (pre != other.pre) return false;
            }
            return true;
        }

        public int CompareTo(Version other)
        {
            if (other == null)
            {
                if (fieldCount == 0)
                    return 0;
                return 1;
            }

            if (major != other.major)
            {
                return major > other.major ? 1 : -1;
            }

            if ((fieldCount > 1 || other.fieldCount > 1) && minor != other.minor)
            {
                return minor > other.minor ? 1 : -1;
            }

            if ((fieldCount > 2 || other.fieldCount > 2) && build != other.build)
            {
                return build > other.build ? 1 : -1;
            }
            if ((fieldCount > 3 || other.fieldCount > 3) && revision != other.revision)
            {
                return revision > other.revision ? 1 : -1;
            }

            if (hasPre || other.hasPre)
            {
                if (preId == other.preId)
                {
                    if (pre != other.pre)
                    {
                        return pre > other.pre ? 1 : -1;
                    }
                }
            }

            return 0;
        }

        public Version Increment()
        {
            if (fieldCount == 0)
                throw new Exception("Field count 0");
            return Increment(fieldCount - 1);
        }

        public Version Increment(int field)
        {
            if (field < 0 || field >= fieldCount)
                throw new IndexOutOfRangeException($"field {field}, field count: {fieldCount}");
            return _Increment(field, false, null, null);
        }

        public Version PreIncrement(int field, string preId, string preSeparator = SEPARATOR)
        {
            if (preId == null) throw new ArgumentNullException(nameof(preId));
            if (preSeparator == null) throw new ArgumentNullException(nameof(preSeparator));
            if ( field >= fieldCount)
                throw new IndexOutOfRangeException($"field {field}, field count: {fieldCount}");
            return _Increment(field, true, preId, preSeparator);
        }

        private Version _Increment(int fieldIndex, bool isPre, string preId, string preSeparator)
        {
            if (isPre)
            {
                if (string.IsNullOrEmpty(preId))
                    throw new ArgumentNullException(nameof(preId));
            }

            Version newVersion = new Version(this);


            if (fieldIndex != -1)
            {
                newVersion[fieldIndex]++;
                //子版本号重置为0
                for (int i = fieldIndex + 1; i < fieldCount; i++)
                {
                    newVersion[i] = 0;
                }
            }

            //预发布版本号
            if (isPre)
            {
                if (fieldIndex != -1)
                {
                    newVersion.pre = 0;
                }
                else
                {
                    if (newVersion.hasPre && newVersion.preId == preId)
                    {
                        //preId 相同则继续递增
                        newVersion.pre++;
                    }
                    else
                    {
                        //preId 不相同则重置, 同时增加最后一位版本号
                        newVersion.pre = 0;
                        newVersion[newVersion.fieldCount - 1]++;
                    }
                }
                newVersion.preId = preId;
                newVersion.preSeparator = preSeparator;
                newVersion.hasPre = true;
            }
            else
            {
                newVersion.pre = 0;
                newVersion.preId = null;
                newVersion.preSeparator = null;
                newVersion.hasPre = false;
            }

            return newVersion;
        }

        public override int GetHashCode()
        {
            if (fieldCount == 0)
                return 0;

            int hash = 0;
            for (int i = 0; i < fieldCount; i++)
            {
                if (i > 0)
                    hash += i;
                hash += this[i];
            }

            if (hasPre)
            {
                hash += preId.GetHashCode();
                hash += preSeparator.GetHashCode();
                hash += pre;
            }

            return hash;
        }

        public override string ToString()
        {
            if (fieldCount == 0)
                return string.Empty;

            StringBuilder builder = new();

            for (int i = 0; i < fieldCount; i++)
            {
                if (i > 0)
                    builder.Append(SEPARATOR);
                builder.Append(this[i]);
            }
            if (hasPre)
            {
                builder.Append(PRE_PREFIX).Append(preId).Append(preSeparator).Append(pre);
            }
            return builder.ToString();
        }

        public static implicit operator string(Version value) => value.ToString();

        public static implicit operator Version(string value) => Parse(value);

        public static bool operator ==(Version lhs, Version rhs)
        {
            if (object.ReferenceEquals(lhs, rhs))
                return true;
            if (object.ReferenceEquals(lhs, null))
            {
                return rhs.Equals(Empty);
            }
            return lhs.Equals(rhs);
        }

        public static bool operator !=(Version lhs, Version rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator >(Version lhs, Version rhs)
        {
            return lhs.CompareTo(rhs) > 0;
        }
        public static bool operator >=(Version lhs, Version rhs)
        {
            return lhs.CompareTo(rhs) >= 0;
        }
        public static bool operator <(Version lhs, Version rhs)
        {
            return lhs.CompareTo(rhs) < 0;
        }
        public static bool operator <=(Version lhs, Version rhs)
        {
            return lhs.CompareTo(rhs) <= 0;
        }


        public enum IncrementField
        {
            None = 0,
            Major,
            Minor,
            Build,
            Revision,
            PreMajor,
            PreMinor,
            PreBuild,
            PreRevision,
            Pre,
        }
    }
}
