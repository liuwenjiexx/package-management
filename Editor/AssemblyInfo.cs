using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.PackageManagement;

#region Unity Package

[assembly: AssemblyMetadata("Package.Name", "com.unity.package-management")]
[assembly: AssemblyMetadata("Unity.Package.Name", "com.unity.package-management")]
[assembly: AssemblyMetadata("Define", "PACKAGE_MANAGEMENT_DEBUG")]

#endregion

[assembly: InternalsVisibleTo("PackageManagement.Tests.Editor")]

