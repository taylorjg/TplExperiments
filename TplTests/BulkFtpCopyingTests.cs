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

            MyDebug.Log("Calling bulkFtpCopyManager.CopyFiles...");
            await bulkFtpCopyManager.CopyFiles3(fileNames, sourceDirectoryFtpPath, targetDirectoryFtpPaths);
            MyDebug.Log("Returned from bulkFtpCopyManager.CopyFiles");

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

            MyDebug.Log("Calling bulkFtpCopyManager.CopyFiles...");
            await bulkFtpCopyManager.CopyFiles3(fileNames, sourceDirectoryFtpPath, targetDirectoryFtpPaths);
            MyDebug.Log("Returned from bulkFtpCopyManager.CopyFiles");

            // Assert
            AssertFilesHaveBeenCopiedCorrectly(fileNames, sourceDirectoryUncPath, targetDirectoryUncPaths);
        }

        [Test]
        public void CallingContinueWithMultipleTimes()
        {
            var startingTask = new Task(() => MyDebug.Log("Inside starting task"));
            var c1 = startingTask.ContinueWith(_ => MyDebug.Log("Inside first continuation"));
            var c2 = startingTask.ContinueWith(_ => MyDebug.Log("Inside second continuation"));
            var c3 = startingTask.ContinueWith(_ => MyDebug.Log("Inside third continuation"));
            startingTask.Start();
            Task.WhenAll(c1, c2, c3).Wait();
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

        // ReSharper disable LoopCanBeConvertedToQuery
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
                        MyDebug.Log("Starting to copy download response stream...");
                        var destinationStream = new MemoryStream();
                        var wrappedDestinationStream = new StreamWrapper(destinationStream, sourceDirectory, fileName);
                        // ReSharper disable PossibleNullReferenceException
                        await downloadResponseStream.CopyToAsync(wrappedDestinationStream);
                        // ReSharper restore PossibleNullReferenceException
                        MyDebug.Log("Done copying download response stream");

                        var buffer = destinationStream.ToArray();
                        wrappedDestinationStream.Close();
                        MyDebug.Log("buffer.Length: {0}", buffer.Length);

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
                                        MyDebug.Log(string.Format("Starting to copy upload request stream... ({0})", uploadState2.TargetDirectory));
                                        var copyToAsyncTask = uploadState2.BufferStream.CopyToAsync(uploadState2.RequestStream);
                                        return copyToAsyncTask;
                                    }, uploadState1).ContinueWith((t, s) =>
                                        {
                                            var uploadState3 = (UploadState) s;
                                            MyDebug.Log(string.Format("Done copying upload request stream ({0})", uploadState3.TargetDirectory));
                                            if (uploadState3.RequestStream != null)
                                            {
                                                uploadState3.RequestStream.Close();
                                                uploadState3.BufferStream.Close();
                                                MyDebug.Log(string.Format("Starting to get upload response stream... ({0})", uploadState3.TargetDirectory));
                                                return uploadState3.FtpWebRequest.GetResponseAsync();
                                            }
                                            return Task<WebResponse>.Factory.StartNew(() => null);
                                        }, uploadState1).Unwrap().ContinueWith((t, s) =>
                                            {
                                                var uploadState4 = (UploadState) s;
                                                MyDebug.Log(string.Format("Done getting upload response stream ({0})", uploadState4.TargetDirectory));
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
        // ReSharper restore LoopCanBeConvertedToQuery

        private class FileCopyState
        {
            public string SourceDirectory { private get; set; }
            public string TargetDirectory { private get; set; }
            public string FileName { private get; set; }
            private FtpWebRequest DownloadFtpWebRequest { get; set; }
            private FtpWebResponse DownloadFtpWebResponse { get; set; }
            private FtpWebRequest UploadFtpWebRequest { get; set; }
            private FtpWebResponse UploadFtpWebResponse { get; set; }
            private Stream DownloadResponseStream { get; set; }
            private MemoryStream DownloadDestinationStream { get; set; }
            private Stream WrappedDownloadDestinationStream { get; set; }
            private Byte[] Buffer { get; set; }
            private Stream WrappedUploadRequestStream { get; set; }
            private Stream UploadSourceStream { get; set; }

            public Task<WebResponse> StartFtpDownload()
            {
                MyDebug.Log("StartFtpDownload - SourceDirectory: {0}; FileName: {1}", SourceDirectory, FileName);
                DownloadFtpWebRequest = CreateFtpWebRequest(SourceDirectory, FileName, WebRequestMethods.Ftp.DownloadFile);
                return DownloadFtpWebRequest.GetResponseAsync();
            }

            public Task ReadDownloadResponseStream(FtpWebResponse ftpWebResponse)
            {
                MyDebug.Log("ReadDownloadResponseStream - SourceDirectory: {0}; FileName: {1}", SourceDirectory, FileName);
                DownloadFtpWebResponse = ftpWebResponse;
                DownloadResponseStream = DownloadFtpWebResponse.GetResponseStream();
                DownloadDestinationStream = new MemoryStream();
                WrappedDownloadDestinationStream = new StreamWrapper(DownloadDestinationStream, SourceDirectory, FileName);
                // ReSharper disable PossibleNullReferenceException
                return DownloadResponseStream.CopyToAsync(WrappedDownloadDestinationStream);
                // ReSharper restore PossibleNullReferenceException
            }

            public Task<Stream> StartFtpUpload()
            {
                MyDebug.Log("StartFtpUpload - TargetDirectory: {0}; FileName: {1}", TargetDirectory, FileName);
                WrappedDownloadDestinationStream.Close();
                Buffer = DownloadDestinationStream.ToArray();
                DownloadFtpWebResponse.Close();
                UploadFtpWebRequest = CreateFtpWebRequest(TargetDirectory, FileName, WebRequestMethods.Ftp.UploadFile);
                return UploadFtpWebRequest.GetRequestStreamAsync();
            }

            public Task WriteUploadRequestStream(Stream uploadRequestStream)
            {
                MyDebug.Log("WriteUploadRequestStream - TargetDirectory: {0}; FileName: {1}", TargetDirectory, FileName);
                WrappedUploadRequestStream = new StreamWrapper(uploadRequestStream, TargetDirectory, FileName);
                UploadSourceStream = new MemoryStream(Buffer);
                return UploadSourceStream.CopyToAsync(WrappedUploadRequestStream);
            }

            public Task<WebResponse> FinishFtpUpload()
            {
                MyDebug.Log("FinishFtpUpload - TargetDirectory: {0}; FileName: {1}", TargetDirectory, FileName);
                return UploadFtpWebRequest.GetResponseAsync();
            }

            public void Cleanup(FtpWebResponse ftpWebResponse)
            {
                MyDebug.Log("Cleanup - TargetDirectory: {0}; FileName: {1}", TargetDirectory, FileName);
                WrappedUploadRequestStream.Close();
                UploadSourceStream.Close();
                UploadFtpWebResponse = ftpWebResponse;
                UploadFtpWebResponse.Close();
            }
        }

        // ReSharper disable LoopCanBeConvertedToQuery
        public async Task CopyFiles2(IEnumerable<string> fileNames, string sourceDirectory, IList<string> targetDirectories)
        {
            var tasks = new List<Task>();

            foreach (var fileName in fileNames)
            {
                foreach (var targetDirectory in targetDirectories)
                {
                    var fileCopyState = new FileCopyState
                        {
                            SourceDirectory = sourceDirectory,
                            TargetDirectory = targetDirectory,
                            FileName = fileName
                        };

                    var task = fileCopyState.StartFtpDownload().ContinueWith((t, s) =>
                        {
                            var state = (FileCopyState) s;
                            var downloadFtpWebResponse = (FtpWebResponse) t.Result;
                            return state.ReadDownloadResponseStream(downloadFtpWebResponse);
                        }, fileCopyState).Unwrap().ContinueWith((t, s) =>
                            {
                                var state = (FileCopyState) s;
                                return state.StartFtpUpload();
                            }, fileCopyState).Unwrap().ContinueWith((t, s) =>
                                {
                                    var state = (FileCopyState) s;
                                    var uploadRequestStream = t.Result;
                                    return state.WriteUploadRequestStream(uploadRequestStream);
                                }, fileCopyState).Unwrap().ContinueWith((t, s) =>
                                    {
                                        var state = (FileCopyState)s;
                                        return state.FinishFtpUpload();
                                    }, fileCopyState).Unwrap().ContinueWith((t, s) =>
                                        {
                                            var state = (FileCopyState)s;
                                            var uploadFtpWebResponse = (FtpWebResponse)t.Result;
                                            state.Cleanup(uploadFtpWebResponse);
                                        }, fileCopyState);

                    tasks.Add(task);
                }
            }

            await Task.WhenAll(tasks);
        }
        // ReSharper restore LoopCanBeConvertedToQuery

        private class WareHouse
        {
            public WareHouse(string sourceDirectory)
            {
                _sourceDirectory = sourceDirectory;
            }

            public void EnqueueCopyOperation(string targetDirectory, string fileName)
            {
                WareHouseData wareHouseData;
                if (!_dictionary.TryGetValue(fileName, out wareHouseData))
                {
                    wareHouseData = new WareHouseData();
                    _dictionary.Add(fileName, wareHouseData);
                    var startingTask = wareHouseData.CreateStartingTask(_sourceDirectory, fileName);
                    _startingTasks.Add(startingTask);
                }

                var finalTask = wareHouseData.EnqueueCopyOperation(targetDirectory);
                _finalTasks.Add(finalTask);
            }

            public void StartCopyOperations()
            {
                foreach (var task in _startingTasks)
                {
                    task.Start();
                }
            }

            public async Task WaitForCopyOperationsToComplete()
            {
                await Task.WhenAll(_finalTasks);
            }

            private class WareHouseData
            {
                private class FileDownloadState
                {
                    public FileDownloadState(string sourceDirectory, string fileName)
                    {
                        SourceDirectory = sourceDirectory;
                        FileName = fileName;
                    }

                    public string FileName { get; private set; }
                    public Byte[] Buffer { get; private set; }

                    private string SourceDirectory { get; set; }
                    private FtpWebRequest DownloadFtpWebRequest { get; set; }
                    private FtpWebResponse DownloadFtpWebResponse { get; set; }
                    private Stream DownloadResponseStream { get; set; }
                    private MemoryStream DownloadDestinationStream { get; set; }
                    private Stream WrappedDownloadDestinationStream { get; set; }

                    public Task<WebResponse> StartFtpDownload()
                    {
                        MyDebug.Log("StartFtpDownload - SourceDirectory: {0}; FileName: {1}", SourceDirectory, FileName);
                        DownloadFtpWebRequest = CreateFtpWebRequest(SourceDirectory, FileName, WebRequestMethods.Ftp.DownloadFile);
                        return DownloadFtpWebRequest.GetResponseAsync();
                    }

                    public Task ReadDownloadResponseStream(FtpWebResponse ftpWebResponse)
                    {
                        MyDebug.Log("ReadDownloadResponseStream - SourceDirectory: {0}; FileName: {1}", SourceDirectory, FileName);
                        DownloadFtpWebResponse = ftpWebResponse;
                        DownloadResponseStream = DownloadFtpWebResponse.GetResponseStream();
                        DownloadDestinationStream = new MemoryStream();
                        WrappedDownloadDestinationStream = new StreamWrapper(DownloadDestinationStream, SourceDirectory, FileName);
                        // ReSharper disable PossibleNullReferenceException
                        return DownloadResponseStream.CopyToAsync(WrappedDownloadDestinationStream);
                        // ReSharper restore PossibleNullReferenceException
                    }

                    public void Cleanup()
                    {
                        MyDebug.Log("Cleanup - SourceDirectory: {0}; FileName: {1}", SourceDirectory, FileName);
                        WrappedDownloadDestinationStream.Close();
                        Buffer = DownloadDestinationStream.ToArray();
                        DownloadFtpWebResponse.Close();
                    }
                }

                private class FileUploadState
                {
                    public FileUploadState(FileDownloadState fileDownloadState, string targetDirectory)
                    {
                        FileDownloadState = fileDownloadState;
                        TargetDirectory = targetDirectory;
                    }

                    private FileDownloadState FileDownloadState { get; set; }
                    private string TargetDirectory { get; set; }
                    private FtpWebRequest UploadFtpWebRequest { get; set; }
                    private FtpWebResponse UploadFtpWebResponse { get; set; }
                    private Stream WrappedUploadRequestStream { get; set; }
                    private Stream UploadSourceStream { get; set; }

                    public Task<Stream> StartFtpUpload()
                    {
                        MyDebug.Log("StartFtpUpload - TargetDirectory: {0}; FileName: {1}", TargetDirectory, FileDownloadState.FileName);
                        UploadFtpWebRequest = CreateFtpWebRequest(TargetDirectory, FileDownloadState.FileName, WebRequestMethods.Ftp.UploadFile);
                        return UploadFtpWebRequest.GetRequestStreamAsync();
                    }

                    public Task WriteUploadRequestStream(Stream uploadRequestStream)
                    {
                        MyDebug.Log("WriteUploadRequestStream - TargetDirectory: {0}; FileName: {1}", TargetDirectory, FileDownloadState.FileName);
                        WrappedUploadRequestStream = new StreamWrapper(uploadRequestStream, TargetDirectory, FileDownloadState.FileName);
                        UploadSourceStream = new MemoryStream(FileDownloadState.Buffer);
                        return UploadSourceStream.CopyToAsync(WrappedUploadRequestStream);
                    }

                    public Task<WebResponse> FinishFtpUpload()
                    {
                        MyDebug.Log("FinishFtpUpload - TargetDirectory: {0}; FileName: {1}", TargetDirectory, FileDownloadState.FileName);
                        return UploadFtpWebRequest.GetResponseAsync();
                    }

                    public void Cleanup(FtpWebResponse ftpWebResponse)
                    {
                        MyDebug.Log("Cleanup - TargetDirectory: {0}; FileName: {1}", TargetDirectory, FileDownloadState.FileName);
                        WrappedUploadRequestStream.Close();
                        UploadSourceStream.Close();
                        UploadFtpWebResponse = ftpWebResponse;
                        UploadFtpWebResponse.Close();
                    }
                }

                public Task CreateStartingTask(string sourceDirectory, string fileName)
                {
                    _startingTask = new Task(NoOperation);

                    _fileDownloadState = new FileDownloadState(sourceDirectory, fileName);

                    _lastDownloadTask = _startingTask
                        .ContinueWith((_, s) =>
                            {
                                var state = (FileDownloadState)s;
                                return state.StartFtpDownload();
                            }, _fileDownloadState).Unwrap().ContinueWith((t, s) =>
                                {
                                    var state = (FileDownloadState)s;
                                    var ftpWebResponse = (FtpWebResponse)t.Result;
                                    return state.ReadDownloadResponseStream(ftpWebResponse);
                                }, _fileDownloadState).Unwrap().ContinueWith((_, s) =>
                                    {
                                        var state = (FileDownloadState)s;
                                        state.Cleanup();
                                    }, _fileDownloadState);

                    return _startingTask;
                }

                public Task EnqueueCopyOperation(string targetDirectory)
                {
                    var fileUploadState = new FileUploadState(_fileDownloadState, targetDirectory);

                    var lastUploadTask = _lastDownloadTask
                        .ContinueWith((_, s) =>
                            {
                                var state = (FileUploadState) s;
                                return state.StartFtpUpload();
                            }, fileUploadState).Unwrap().ContinueWith((t, s) =>
                                {
                                    var state = (FileUploadState) s;
                                    var requestStream = t.Result;
                                    return state.WriteUploadRequestStream(requestStream);
                                }, fileUploadState).Unwrap().ContinueWith((_, s) =>
                                    {
                                        var state = (FileUploadState) s;
                                        return state.FinishFtpUpload();
                                    }, fileUploadState).Unwrap().ContinueWith((t, s) =>
                                        {
                                            var state = (FileUploadState)s;
                                            var ftpWebResponse = (FtpWebResponse)t.Result;
                                            state.Cleanup(ftpWebResponse);
                                        }, fileUploadState);

                    return lastUploadTask;
                }

                private static void NoOperation()
                {
                }

                private Task _startingTask;
                private FileDownloadState _fileDownloadState;
                private Task _lastDownloadTask;
            }

            private readonly string _sourceDirectory;
            private readonly IDictionary<string, WareHouseData> _dictionary = new Dictionary<string, WareHouseData>();
            private readonly IList<Task> _startingTasks = new List<Task>();
            private readonly IList<Task> _finalTasks = new List<Task>();
        }

        public async Task CopyFiles3(IList<string> fileNames, string sourceDirectory, IList<string> targetDirectories)
        {
            MyDebug.Log("Entering CopyFiles3");

            var wareHouse = new WareHouse(sourceDirectory);

            foreach (var targetDirectory in targetDirectories)
            {
                foreach (var fileName in fileNames)
                {
                    wareHouse.EnqueueCopyOperation(targetDirectory, fileName);
                }
            }

            MyDebug.Log("Calling wareHouse.StartCopyOperations");
            wareHouse.StartCopyOperations();

            MyDebug.Log("Calling wareHouse.WaitForCopyOperationsToComplete");
            await wareHouse.WaitForCopyOperationsToComplete();

            MyDebug.Log("Leaving CopyFiles3");
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
