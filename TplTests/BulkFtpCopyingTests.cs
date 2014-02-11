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
        private const string FtpHostName = "10.10.201.134";
        private const string FtpUserName = "env6ftp";
        private const string FtpPasssword = "w1nd0w5.";

        public async Task CopyFiles(IEnumerable<string> fileNames, string sourceDirectory, IList<string> targetDirectories)
        {
            foreach (var fileName in fileNames)
            {
                var downloadFtpWebRequest = CreateFtpWebRequest(sourceDirectory, fileName, WebRequestMethods.Ftp.DownloadFile);
                using (var downloadFtpWebResponse = await downloadFtpWebRequest.GetResponseAsync())
                {
                    using (var downloadResponseStream = downloadFtpWebResponse.GetResponseStream())
                    {
                        System.Diagnostics.Debug.WriteLine("Starting to copy download response stream...");
                        var destination = new MemoryStream();
                        await downloadResponseStream.CopyToAsync(destination);
                        System.Diagnostics.Debug.WriteLine("Done copying download response stream");

                        foreach (var targetDirectory in targetDirectories)
                        {
                            var uploadFtpWebRequest = CreateFtpWebRequest(targetDirectory, fileName, WebRequestMethods.Ftp.UploadFile);
                            using (var uploadRequestStream = await uploadFtpWebRequest.GetRequestStreamAsync())
                            {
                                destination.Seek(0, SeekOrigin.Begin);
                                System.Diagnostics.Debug.WriteLine("destination.Length: {0}", destination.Length);

                                System.Diagnostics.Debug.WriteLine("Starting to copy upload request stream...");
                                await destination.CopyToAsync(uploadRequestStream);
                                System.Diagnostics.Debug.WriteLine("Done copying upload request stream");

                                await uploadFtpWebRequest.GetResponseAsync();
                            }
                        }
                    }
                }
            }
        }

        private static FtpWebRequest CreateFtpWebRequest(string directory, string fileName, string method)
        {
            var uri = string.Format("ftp://{0}/{1}/{2}", FtpHostName, directory, fileName);
            var ftpWebRequest = (FtpWebRequest)WebRequest.Create(uri);
            ftpWebRequest.Method = method;
            ftpWebRequest.UseBinary = true;
            ftpWebRequest.KeepAlive = true;
            ftpWebRequest.ConnectionGroupName = "BulkFtpCopyingTests";
            ftpWebRequest.Credentials = new NetworkCredential(FtpUserName, FtpPasssword);
            return ftpWebRequest;
        }
    }
}
