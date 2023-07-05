using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CykieAppLauncher.Models;

public record class ConfigurationInfo(string Name, string Version, string ZipPath, string LaunchPath,
string VersionLink, string BuildLink)
{
    public ConfigurationInfo(string[] info) : 
        this(info[0], info[1], info[2], info[3], info[4], info[5]){ }
}

public struct Version : IComparable<Version>
{
    public static Version Invalid { get; } = new Version(-1, -1, -1);
    public static Version Default { get; } = new Version(0, 1, 0);

    short major = 0, minor = 1, patch = 0;
    short? revision = null;

    public Version()
    {
        Set(Default);
    }
    public Version(short major, short minor, short patch = 0, short? revision = null)
    {
        Set(major, minor, patch, revision);
    }
    public Version(string? versionStr)
    {
        if (versionStr == null)
        {
            Set(Invalid);
            return;
        }

        var parts = versionStr.Split('.');

        if (parts.Length < 3)
        {
            Set(Invalid);
            return;
        }

        try
        {
            major = short.Parse(parts[0]);
            minor = short.Parse(parts[1]);
            patch = short.Parse(parts[2]);
            if (parts.Length > 3)
                revision = short.Parse(parts[3]);
        }
        catch (Exception)
        {
            Set(Invalid);
        }
    }

    public void Set(short inMajor, short inMinor, short inPatch = 0, short? inRevision = null)
    {
        major = inMajor;
        minor = inMinor;
        patch = inPatch;
        revision = inRevision;
    }
    public void Set(Version inVersion)
    {
        major = inVersion.major;
        minor = inVersion.minor;
        patch = inVersion.patch;
        revision = inVersion.revision;
    }

    public readonly bool IsValid()
    {
        var v = new Version(ToString());

        if (v.Equals(Invalid))
            return false;
        return true;
    }

    public override readonly string ToString()
    {
        var str = $"{major}.{minor}.{patch}";
        if (revision != null)
            str += $".{revision}";
        return str;
    }

    public readonly int CompareTo(Version other)
    {
        if (Equals(other)) return 0;

        //! Compare with decreasing importance

        if (major != other.major)
            return major > other.major ? 1 : -1;//Greater major means newer version

        //Majors are the same so compare minors
        if (minor != other.minor)
            return minor > other.minor ? 1 : -1;

        //Minors are the same so compare patches
        if (patch != other.patch)
            return patch > other.patch ? 1 : -1;

        if (revision == null)
            return -1;

        if (other.revision == null)
            return 1;

        return revision > other.revision ? 1 : -1;
    }

}
