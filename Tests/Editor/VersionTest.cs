using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;


namespace Unity.PackageManagement.Tests
{

    public class VersionTest
    {

        [Test]
        public void ParseSuccess()
        {
            Version v;
            Assert.IsTrue(Version.TryParse("1", out v));
            Assert.AreEqual(1, v.FieldCount);
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(0, v.Minor);
            Assert.AreEqual(0, v.Build);
            Assert.AreEqual(0, v.Revision);
            Assert.AreEqual(0, v.Pre);
            Assert.IsNull(v.PreId);

            Assert.IsTrue(Version.TryParse("1.2", out v));
            Assert.AreEqual(2, v.FieldCount);
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(2, v.Minor);
            Assert.AreEqual(0, v.Build);
            Assert.AreEqual(0, v.Revision);
            Assert.AreEqual(0, v.Pre);
            Assert.IsNull(v.PreId);

            Assert.IsTrue(Version.TryParse("1.2.3", out v));
            Assert.AreEqual(3, v.FieldCount);
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(2, v.Minor);
            Assert.AreEqual(3, v.Build);
            Assert.AreEqual(0, v.Revision);
            Assert.AreEqual(0, v.Pre);
            Assert.IsNull(v.PreId);

            Assert.IsTrue(Version.TryParse("1.2.3.4", out v));
            Assert.AreEqual(4, v.FieldCount);
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(2, v.Minor);
            Assert.AreEqual(3, v.Build);
            Assert.AreEqual(4, v.Revision);
            Assert.AreEqual(0, v.Pre);
            Assert.IsNull(v.PreId);
        }



        [Test]
        public void ParsePre()
        {
            Version v;
            Assert.IsTrue(Version.TryParse("1.2-pre-1", out v));
            Assert.AreEqual(2, v.FieldCount);
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(2, v.Minor);
            Assert.AreEqual(0, v.Build);
            Assert.AreEqual(0, v.Revision);
            Assert.AreEqual(1, v.Pre);
            Assert.AreEqual("pre", v.PreId);
            Assert.AreEqual("-", v.PreSeparator);

            Assert.IsTrue(Version.TryParse("1.2.3-preview-2", out v));
            Assert.AreEqual(3, v.FieldCount);
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(2, v.Minor);
            Assert.AreEqual(3, v.Build);
            Assert.AreEqual(0, v.Revision);
            Assert.AreEqual(2, v.Pre);
            Assert.AreEqual("preview", v.PreId);
            Assert.AreEqual("-", v.PreSeparator);

            Assert.IsTrue(Version.TryParse("1.2.3.4-alpha-3", out v));
            Assert.AreEqual(4, v.FieldCount);
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(2, v.Minor);
            Assert.AreEqual(3, v.Build);
            Assert.AreEqual(4, v.Revision);
            Assert.AreEqual(3, v.Pre);
            Assert.AreEqual("alpha", v.PreId);
            Assert.AreEqual("-", v.PreSeparator);
        }

        [Test]
        public void PreSeparator()
        {
            Version v;
            Assert.IsTrue(Version.TryParse("1.2-pre-1", out v));
            Assert.AreEqual(2, v.FieldCount);
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(2, v.Minor);
            Assert.AreEqual(0, v.Build);
            Assert.AreEqual(0, v.Revision);
            Assert.AreEqual(1, v.Pre);
            Assert.AreEqual("pre", v.PreId);
            Assert.AreEqual("-", v.PreSeparator);

            Assert.IsTrue(Version.TryParse("1.2.3-pre.2", out v));
            Assert.AreEqual(3, v.FieldCount);
            Assert.AreEqual(1, v.Major);
            Assert.AreEqual(2, v.Minor);
            Assert.AreEqual(3, v.Build);
            Assert.AreEqual(0, v.Revision);
            Assert.AreEqual(2, v.Pre);
            Assert.AreEqual("pre", v.PreId);
            Assert.AreEqual(".", v.PreSeparator);
        }

        [Test]
        public void ParseFailed_Empty()
        {
            Version v;
            Assert.IsFalse(Version.TryParse(string.Empty, out v));
        }

        [Test]
        public void ParseFailed_FieldOverflow()
        {
            Version v;
            Assert.IsFalse(Version.TryParse("1.2.3.4.5", out v));
        }

        [Test]
        public void ParseFailed_NotInteger()
        {
            Version v;
            Assert.IsFalse(Version.TryParse("a.b", out v));
        }

        [Test]
        public void VersionString()
        {
            Assert.AreEqual(string.Empty, Version.Empty.ToString());

            Version v;

            v = Version.Parse("1.0");
            Assert.AreEqual("1.0", v.ToString());

            v = Version.Parse("1.2");
            Assert.AreEqual("1.2", v.ToString());

            v = Version.Parse("1.2.3");
            Assert.AreEqual("1.2.3", v.ToString());

            v = Version.Parse("1.2.3.4");
            Assert.AreEqual("1.2.3.4", v.ToString());

            v = Version.Parse("1.2-pre-0");
            Assert.AreEqual("1.2-pre-0", v.ToString());

            v = Version.Parse("1.2-pre-1");
            Assert.AreEqual("1.2-pre-1", v.ToString());

            v = Version.Parse("1.2-pre.1");
            Assert.AreEqual("1.2-pre.1", v.ToString());
        }

        [Test]
        public void Equal()
        {
            Version v1;
            Version v2;

            v1 = null;
            v2 = null;

            Assert.IsTrue(v1 == v2);

            v1 = Version.Empty;
            Assert.IsTrue(v1 == null);


            v1 = Version.Parse("1.2");
            v2 = Version.Parse("1.2");
            Assert.IsTrue(v1.Equals(v2));
            Assert.IsTrue(v1 == v2);


            v1 = Version.Parse("1.2-pre-1");
            v2 = Version.Parse("1.2-pre-1");
            Assert.IsTrue(v1.Equals(v2));
            Assert.IsTrue(v1 == v2);

        }
        [Test]
        public void NotEqual()
        {
            Version v1;
            Version v2;

            v1 = Version.Parse("1.0");
            v2 = Version.Parse("1.2");
            Assert.IsFalse(v1.Equals(v2));
            Assert.IsFalse(v1 == v2);


            v1 = Version.Parse("1.0-pre-1");
            v2 = Version.Parse("1.2-pre-1");
            Assert.IsFalse(v1.Equals(v2));
            Assert.IsFalse(v1 == v2);

            v1 = Version.Parse("1.0-pre-1");
            v2 = Version.Parse("1.0-pre-2");
            Assert.IsFalse(v1.Equals(v2));
            Assert.IsFalse(v1 == v2);


            v1 = Version.Parse("1.0-pre-1");
            v2 = Version.Parse("1.0-preview-1");
            Assert.IsFalse(v1.Equals(v2));
            Assert.IsFalse(v1 == v2);
        }


        [Test]
        public void Compare_Less()
        {
            Version v1;
            Version v2;

            v1 = Version.Parse("1.0");
            v2 = Version.Parse("1.2");
            Assert.IsTrue(v1.CompareTo(v2) < 0);
            Assert.IsTrue(v1 < v2);
            Assert.IsFalse(v2 < v1);

            v1 = Version.Parse("1.0.0");
            v2 = Version.Parse("1.2.3");
            Assert.IsTrue(v1.CompareTo(v2) < 0);
            Assert.IsTrue(v1 < v2);
            Assert.IsFalse(v2 < v1);


            v1 = Version.Parse("1.0-pre-0");
            v2 = Version.Parse("1.0-pre-1");
            Assert.IsTrue(v1.CompareTo(v2) < 0);
            Assert.IsTrue(v1 < v2);


            v1 = Version.Parse("1.0-pre-0");
            v2 = Version.Parse("1.2-pre-0");
            Assert.IsTrue(v1.CompareTo(v2) < 0);
            Assert.IsTrue(v1 < v2);

            v1 = Version.Parse("1.0-pre-0");
            v2 = Version.Parse("1.2-pre-1");
            Assert.IsTrue(v1.CompareTo(v2) < 0);
            Assert.IsTrue(v1 < v2);


            v1 = Version.Parse("1.0-pre-1");
            v2 = Version.Parse("1.2-pre-0");
            Assert.IsTrue(v1.CompareTo(v2) < 0);
            Assert.IsTrue(v1 < v2);
        }

        [Test]
        public void Compare_LessOrEqual()
        {
            Version v1;
            Version v2;
            v1 = Version.Parse("1.0");
            v2 = Version.Parse("1.0");

            Assert.IsTrue(v1.CompareTo(v2) == 0);
            Assert.IsTrue(v1 <= v2);
            Assert.IsTrue(v2 <= v1);


            v1 = Version.Parse("1.0-pre-0");
            v2 = Version.Parse("1.0-preview-1");
            Assert.IsTrue(v1.CompareTo(v2) <= 0);
            Assert.IsTrue(v1 <= v2);

        }

        [Test]
        public void Compare_Greater()
        {
            Version v1;
            Version v2;

            v1 = Version.Parse("1.2");
            v2 = Version.Parse("1.0");
            Assert.IsTrue(v1.CompareTo(v2) > 0);
            Assert.IsTrue(v1 > v2);
            Assert.IsFalse(v2 > v1);

            v1 = Version.Parse("1.2.3");
            v2 = Version.Parse("1.0.0");
            Assert.IsTrue(v1.CompareTo(v2) > 0);
            Assert.IsTrue(v1 > v2);
            Assert.IsFalse(v2 > v1);

        }

        [Test]
        public void Compare_GreaterOrEqual()
        {
            Version v1;
            Version v2;

            v1 = Version.Parse("1.0");
            v2 = Version.Parse("1.0");

            Assert.IsTrue(v1.CompareTo(v2) == 0);
            Assert.IsTrue(v1 >= v2);
            Assert.IsTrue(v2 >= v1);
        }

        string Increment(string version, int field)
        {
            return Version.Parse(version).Increment(field).ToString();
        }

        [Test]
        public void Increment()
        {
            Version v;
            v = Version.Parse("1.0");
            Assert.AreEqual("2.0", v.Increment(Version.MAJOR).ToString());
            Assert.AreEqual("1.0", v.ToString());

            Assert.AreEqual("2.0", Increment("1.0", Version.MAJOR));
            Assert.AreEqual("1.1", Increment("1.0", Version.MINOR));

            Assert.AreEqual("2.0.0.0", Increment("1.0.0.0", Version.MAJOR));
            Assert.AreEqual("1.1.0.0", Increment("1.0.0.0", Version.MINOR));
            Assert.AreEqual("1.0.1.0", Increment("1.0.0.0", Version.BUILD));
            Assert.AreEqual("1.0.0.1", Increment("1.0.0.0", Version.REVISION));
        }

        string PreIncrement(string version, int field, string preId = "pre", string preSeparator = Version.SEPARATOR)
        {
            return Version.Parse(version).PreIncrement(field, preId, preSeparator).ToString();
        }

        string PreIncrement(string version, string preId = "pre", string preSeparator = Version.SEPARATOR)
        {
            return Version.Parse(version).PreIncrement(Version.NONE, preId, preSeparator).ToString();
        }

        [Test]
        public void PreIncrementField()
        {
            Version v;

            v = Version.Parse("1.0");
            Assert.AreEqual("2.0-pre.0", v.PreIncrement(Version.MAJOR, "pre", ".").ToString());
            Assert.AreEqual("2.0-pre-0", v.PreIncrement(Version.MAJOR, "pre", "-").ToString());
            Assert.AreEqual("2.0-preview.0", v.PreIncrement(Version.MAJOR, "preview", ".").ToString());
            Assert.AreEqual("1.0", v.ToString());

            Assert.AreEqual("2.0-pre.0", PreIncrement("1.0", Version.MAJOR));
            Assert.AreEqual("1.1-pre.0", PreIncrement("1.0", Version.MINOR));
            Assert.AreEqual("3.0-pre.0", PreIncrement("2.0-pre.0", Version.MAJOR));

            v = Version.Parse("1.0.0.0");
            Assert.AreEqual("2.0.0.0-pre.0", PreIncrement("1.0.0.0", Version.MAJOR));
            Assert.AreEqual("1.1.0.0-pre.0", PreIncrement("1.0.0.0", Version.MINOR));
            Assert.AreEqual("1.0.1.0-pre.0", PreIncrement("1.0.0.0", Version.BUILD));
            Assert.AreEqual("1.0.0.1-pre.0", PreIncrement("1.0.0.0", Version.REVISION));
        }


        [Test]
        public void PreIncrement()
        {
            Assert.AreEqual("1.1-pre.0", PreIncrement("1.0"));
            Assert.AreEqual("1.0-pre.1", PreIncrement("1.0-pre.0"));
            Assert.AreEqual("1.0-pre.2", PreIncrement("1.0-pre.1"));
        }

        int HashCode(string version)
        {
            return Version.Parse(version).GetHashCode();
        }

        [Test]
        public void HashCode()
        {
            Assert.AreEqual(0, Version.Empty.GetHashCode());
            Assert.AreNotEqual(0, HashCode("1"));
            Assert.AreNotEqual(HashCode("1"), HashCode("1.0"));
            Assert.AreNotEqual(HashCode("1.0"), HashCode("1.0-pre.0"));
        }
    }
}