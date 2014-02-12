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
        private static readonly Tuple<string, string> TargetDirectoryPair2 = Tuple.Create(@"LoopMonTest/TargetDir2", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir2");
        private static readonly Tuple<string, string> TargetDirectoryPair3 = Tuple.Create(@"LoopMonTest/TargetDir3", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir3");

        [SetUp]
        public void SetUp()
        {
            ServicePointManager.DefaultConnectionLimit = 100;
        }

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
            System.Diagnostics.Debug.WriteLine("Calling bulkFtpCopyManager.CopyFiles...");
            await bulkFtpCopyManager.CopyFiles(fileNames, sourceDirectoryFtpPath, targetDirectoryFtpPaths);
            System.Diagnostics.Debug.WriteLine("Returned from bulkFtpCopyManager.CopyFiles");

            // Assert
            AssertFilesHaveBeenCopiedCorrectly(fileNames, sourceDirectoryUncPath, targetDirectoryUncPaths);
        }

        [Test]
        public async void CopyingOneFileToThreeTargetDirectories()
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
                    TargetDirectoryPair1,
                    TargetDirectoryPair2,
                    TargetDirectoryPair3
                };
            var targetDirectoryFtpPaths = targetDirectoryPairs.Select(x => x.Item1).ToList();
            var targetDirectoryUncPaths = targetDirectoryPairs.Select(x => x.Item2).ToList();

            // Act
            DeleteAllFilesInDirectories(targetDirectoryUncPaths);
            var bulkFtpCopyManager = new BulkFtpCopyManager();
            System.Diagnostics.Debug.WriteLine("Calling bulkFtpCopyManager.CopyFiles...");
            await bulkFtpCopyManager.CopyFiles(fileNames, sourceDirectoryFtpPath, targetDirectoryFtpPaths);
            System.Diagnostics.Debug.WriteLine("Returned from bulkFtpCopyManager.CopyFiles");

            // Assert
            AssertFilesHaveBeenCopiedCorrectly(fileNames, sourceDirectoryUncPath, targetDirectoryUncPaths);
        }

        private static void DeleteAllFilesInDirectories(IEnumerable<string> directories)
        {
            foreach (var directory in directories)
            {
                DeleteAllFilesInDirectory(directory);
            }

            FudgedWaitForFileSystemToSettleDown();
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
            FudgedWaitForFileSystemToSettleDown();

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

        private static void FudgedWaitForFileSystemToSettleDown()
        {
            System.Threading.Thread.Sleep(5 * 1000);
        }
    }

    internal class BulkFtpCopyManager
    {
        private const string FtpHostName = "10.10.201.134";
        private const string FtpUserName = "env6ftp";
        private const string FtpPasssword = "w1nd0w5.";

        private class UploadState
        {
            public string TargetDirectory { get; set; }
            public string FileName { get; set; }
            public FtpWebRequest FtpWebRequest { get; set; }
            public FtpWebResponse FtpWebResponse { get; set; }
            public Stream BufferStream { get; set; }
            public Stream RequestStream { get; set; }
        }

        public async Task CopyFiles(IEnumerable<string> fileNames, string sourceDirectory, IList<string> targetDirectories)
        {
            var tasks = new List<Task>();

            foreach (var fileName in fileNames)
            {
                var downloadFtpWebRequest = CreateFtpWebRequest(sourceDirectory, fileName, WebRequestMethods.Ftp.DownloadFile);
                using (var downloadFtpWebResponse = await downloadFtpWebRequest.GetResponseAsync())
                {
                    using (var downloadResponseStream = downloadFtpWebResponse.GetResponseStream())
                    {
                        System.Diagnostics.Debug.WriteLine("Starting to copy download response stream...");
                        var destinationStream = new MemoryStream();
                        var wrappedDestinationStream = new StreamWrapper(destinationStream, sourceDirectory, fileName);
                        await downloadResponseStream.CopyToAsync(wrappedDestinationStream);
                        System.Diagnostics.Debug.WriteLine("Done copying download response stream");

                        var buffer = destinationStream.ToArray();
                        wrappedDestinationStream.Close();
                        System.Diagnostics.Debug.WriteLine("buffer.Length: {0}", buffer.Length);

                        foreach (var targetDirectory in targetDirectories)
                        {
                            var uploadFtpWebRequest = CreateFtpWebRequest(targetDirectory, fileName, WebRequestMethods.Ftp.UploadFile);
                            var uploadTask = uploadFtpWebRequest.GetRequestStreamAsync();
                            var uploadState1 = new UploadState
                                {
                                    TargetDirectory = targetDirectory,
                                    FileName = fileName,
                                    FtpWebRequest = uploadFtpWebRequest
                                };
                            var overallUploadTask = uploadTask
                                .ContinueWith((t, s) =>
                                    {
                                        var uploadState2 = (UploadState) s;
                                        uploadState2.RequestStream = new StreamWrapper(t.Result, uploadState2.TargetDirectory, uploadState2.FileName);
                                        uploadState2.BufferStream = new MemoryStream(buffer);
                                        System.Diagnostics.Debug.WriteLine(string.Format("Starting to copy upload request stream... ({0})", uploadState2.TargetDirectory));
                                        var copyToAsyncTask = uploadState2.BufferStream.CopyToAsync(uploadState2.RequestStream);
                                        return copyToAsyncTask;
                                    }, uploadState1).ContinueWith((t, s) =>
                                        {
                                            var uploadState3 = (UploadState) s;
                                            System.Diagnostics.Debug.WriteLine(string.Format("Done copying upload request stream ({0})", uploadState3.TargetDirectory));
                                            if (uploadState3.RequestStream != null)
                                            {
                                                uploadState3.RequestStream.Close();
                                                uploadState3.BufferStream.Close();
                                                System.Diagnostics.Debug.WriteLine(string.Format("Starting to get upload response stream... ({0})", uploadState3.TargetDirectory));
                                                return uploadState3.FtpWebRequest.GetResponseAsync();
                                            }
                                            return Task<WebResponse>.Factory.StartNew(() => null);
                                        }, uploadState1).Unwrap().ContinueWith((t, s) =>
                                            {
                                                var uploadState4 = (UploadState) s;
                                                System.Diagnostics.Debug.WriteLine(string.Format("Done getting upload response stream ({0})", uploadState4.TargetDirectory));
                                                uploadState4.FtpWebResponse = (FtpWebResponse) t.Result;
                                                uploadState4.FtpWebResponse.Close();
                                            }, uploadState1);
                            tasks.Add(overallUploadTask);
                        }
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task CopyFiles2(IEnumerable<string> fileNames, string sourceDirectory, IList<string> targetDirectories)
        {
            var tasks = new List<Task>();

            foreach (var fileName in fileNames)
            {
            }

            await Task.WhenAll(tasks);
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
