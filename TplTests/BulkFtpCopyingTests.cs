using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TplTests
{
    [TestFixture]
    internal class BulkFtpCopyingTests
    {
        private static readonly Tuple<string, string> SourceDirectoryPair = Tuple.Create (@"LoopMonTest/SourceDir",  @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\SourceDir");
        private static readonly Tuple<string, string> TargetDirectoryPair1 = Tuple.Create(@"LoopMonTest/TargetDir1", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir1");
        //private static readonly Tuple<string, string> TargetDirectoryPair2 = Tuple.Create(@"LoopMonTest/TargetDir2", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir2");
        //private static readonly Tuple<string, string> TargetDirectoryPair3 = Tuple.Create(@"LoopMonTest/TargetDir3", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir3");

        [Test]
        public async void CopyingOneFileToOneTargetDirectory()
        {
            // Arrange
            var fileNames = new[]
                {
                    "combined_Master_F217307845EECA800B75936A37BCA697.js"
                };

            var sourceDirectoryFtpPath = SourceDirectoryPair.Item1;
            var sourceDirectoryUncPath = SourceDirectoryPair.Item2;

            var targetDirectoryPairs = new[]
                {
                    TargetDirectoryPair1
                };
            var targetDirectoryFtpPaths = targetDirectoryPairs.Select(x => x.Item1).ToList();
            var targetDirectoryUncPaths = targetDirectoryPairs.Select(x => x.Item2).ToList();

            // Act
            DeleteAllFilesInDirectories(targetDirectoryUncPaths);
            var bulkFtpCopyManager = new BulkFtpCopyManager();
            await bulkFtpCopyManager.CopyFiles(fileNames, sourceDirectoryFtpPath, targetDirectoryFtpPaths);

            // Assert
            AssertFilesHaveBeenCopiedCorrectly(fileNames, sourceDirectoryUncPath, targetDirectoryUncPaths);
        }

        private static void DeleteAllFilesInDirectories(IEnumerable<string> directories)
        {
            foreach (var directory in directories)
            {
                DeleteAllFilesInDirectory(directory);
            }
        }

        private static void DeleteAllFilesInDirectory(string directory)
        {
            var files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        private static void AssertFilesHaveBeenCopiedCorrectly(string[] fileNames, string sourceDirectory, IEnumerable<string> targetDirectories)
        {
            var dictionary = new Dictionary<string, byte[]>();
            foreach (var fileName in fileNames)
            {
                var path = Path.Combine(sourceDirectory, fileName);
                var buffer = File.ReadAllBytes(path);
                dictionary[fileName] = buffer;
            }

            foreach (var targetDirectory in targetDirectories)
            {
                foreach (var fileName in fileNames)
                {
                    var path = Path.Combine(targetDirectory, fileName);
                    var targetBuffer = File.ReadAllBytes(path);
                    var sourceBuffer = dictionary[fileName];
                    Assert.That(targetBuffer, Is.EqualTo(sourceBuffer));
                }
            }
        }
    }

    internal class BulkFtpCopyManager
    {
        private const string FtpUserName = "";
        private const string FtpPasssword = "";

        public async Task CopyFiles(IEnumerable<string> fileNames, string sourceDirectory, IEnumerable<string> targetDirectories)
        {
            // init data structures
            // for each targetDirectory
            //  enqueue an async upload operation from sourceDirectory\filename to targetDirectory\filename
            // end for
            // wait for all async operations to complete

            var targetDirectory = targetDirectories.First();

            foreach (var fileName in fileNames)
            {
                var uri1 = "ftp://10.10.201.134/" + sourceDirectory + "/" + fileName;
                var ftpWebRequest1 = (FtpWebRequest) WebRequest.Create(uri1);
                ftpWebRequest1.Method = WebRequestMethods.Ftp.DownloadFile;
                ftpWebRequest1.UseBinary = true;
                ftpWebRequest1.KeepAlive = true;
                ftpWebRequest1.ConnectionGroupName = "BulkFtpCopyingTests";
                ftpWebRequest1.Credentials = new NetworkCredential(FtpUserName, FtpPasssword);

                var destination = new MemoryStream();

                using (var response1 = await ftpWebRequest1.GetResponseAsync())
                {
                    using (var responseStream1 = response1.GetResponseStream())
                    {
                        System.Diagnostics.Debug.WriteLine("Starting to copy download response stream...");
                        await responseStream1.CopyToAsync(destination);
                        System.Diagnostics.Debug.WriteLine("Done copying download response stream");

                        var uri2 = "ftp://10.10.201.134/" + targetDirectory + "/" + fileName;
                        var ftpWebRequest2 = (FtpWebRequest)WebRequest.Create(uri2);
                        ftpWebRequest2.Method = WebRequestMethods.Ftp.UploadFile;
                        ftpWebRequest2.UseBinary = true;
                        ftpWebRequest2.KeepAlive = true;
                        ftpWebRequest2.ConnectionGroupName = "BulkFtpCopyingTests";
                        ftpWebRequest2.Credentials = new NetworkCredential(FtpUserName, FtpPasssword);

                        using (var requestStream2 = await ftpWebRequest2.GetRequestStreamAsync())
                        {
                            destination.Seek(0, SeekOrigin.Begin);
                            System.Diagnostics.Debug.WriteLine("destination.Length: {0}", destination.Length);

                            System.Diagnostics.Debug.WriteLine("Starting to copy upload request stream...");
                            await destination.CopyToAsync(requestStream2);
                            System.Diagnostics.Debug.WriteLine("Done copying upload request stream");

                            await ftpWebRequest2.GetResponseAsync();
                        }
                    }
                }
            }
        }
    }
}
