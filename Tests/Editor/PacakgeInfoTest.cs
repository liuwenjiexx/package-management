using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.PackageManagement.Tests
{

    public class PacakgeInfoTest
    {
        string testPackageJson = @"{
  ""name"": ""com.example"",
  ""displayName"": ""Example"",
  ""version"": ""1.0.0"",
  ""unity"": ""2021.3"",
  ""hideInEditor"": true,
  ""samples"": [],
  ""author"": {
    ""name"": ""Test""
  },
  ""dependencies"": {
    ""com.example.dep"": ""0.0.1""
  }
}";
        [Test]
        public void ReplaceDisplayname()
        {
            string newText = PackageInfo.ReplaceProperties(testPackageJson, new()
            {
                { PackageInfo.Properties.DisplayName ,"TestName"},
            });
            Debug.Log(newText);
            var dic = PackageInfo.ParseProperties(newText);
            Assert.IsTrue(dic.TryGetValue(PackageInfo.Properties.DisplayName, out var value));
            Assert.AreEqual("TestName", value);
        }

        [Test]
        public void ReplaceValues()
        {
            string newText = PackageInfo.ReplaceProperties(testPackageJson, new()
            {
                { PackageInfo.Properties.DisplayName ,"TestName"},
                { PackageInfo.Properties.Version ,"1.0-test-0"},
            });
            Debug.Log(newText);
            var dic = PackageInfo.ParseProperties(newText);
            Assert.IsTrue(dic.TryGetValue(PackageInfo.Properties.DisplayName, out var value));
            Assert.AreEqual("TestName", value);
            Assert.IsTrue(dic.TryGetValue(PackageInfo.Properties.Version, out value));
            Assert.AreEqual("1.0-test-0", value);
        }
    }
}
