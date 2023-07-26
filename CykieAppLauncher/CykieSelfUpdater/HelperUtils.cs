using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace CykieAppLauncher.Models
{
    //https://stackoverflow.com/questions/45711428/download-file-with-webclient-or-httpclient
    public static class HelperUtils
    {
        /// <summary>
        /// If this object is equal to the exclusion, then set it to the specified value
        /// </summary>
        /// <param name="exclusion">The value to check myself against</param>
        /// <param name="conditionalValue">The value to switch too if I'm equal to the exclusion</param>
        /// <returns></returns>
        public static T Unless<T>(this T self, T exclusion, T conditionalValue)
        {
            if (self == null)
                return exclusion == null ? conditionalValue : self;

            return self.Equals(exclusion) ? conditionalValue : self;
        }

        //? This cause errors when imported into Unity
        /*public static async Task DownloadFileTaskAsync(this HttpClient client, Uri uri, string fileName)
        {
            using var downloadStream = await client.GetStreamAsync(uri);
            using var fileStream = File.Create(fileName);

            await downloadStream.CopyToAsync(fileStream);
        }*/

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
}