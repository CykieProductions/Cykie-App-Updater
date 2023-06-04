using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace CykieAppLauncher.Models;

//https://stackoverflow.com/questions/45711428/download-file-with-webclient-or-httpclient
public static class HelperUtils
{
    public static async Task DownloadFileTaskAsync(this HttpClient client, Uri uri, string fileName)
    {
        using var downloadStream = await client.GetStreamAsync(uri);
        using var fileStream = File.Create(fileName);

        await downloadStream.CopyToAsync(fileStream);
    }

    public static bool IsZipValid(string path)
    {
        try
        {
            using var zipFile = ZipFile.OpenRead(path);
            var entries = zipFile.Entries;
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    //https://stackoverflow.com/questions/12553809/how-to-check-whether-file-exists-in-zip-file-using-dotnetzip
    public static bool HasFile(this ZipArchive thisArchive, string fileNameOrExtension)
    {
        foreach (ZipArchiveEntry entry in thisArchive.Entries)
        {
            if (entry.FullName.EndsWith(fileNameOrExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}