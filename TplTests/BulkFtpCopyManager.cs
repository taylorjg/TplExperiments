using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace TplTests
{
    internal class BulkFtpCopyManager
    {
        private const string FtpHostName = "10.10.201.134";
        private const string FtpUserName = "env6ftp";
        private const string FtpPasssword = "w1nd0w5.";

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

        public async Task CopyFiles(IList<string> fileNames, string sourceDirectory, IList<string> targetDirectories)
        {
            MyDebug.Log("Entering CopyFiles");

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

            MyDebug.Log("Leaving CopyFiles");
        }

        private static FtpWebRequest CreateFtpWebRequest(string directory, string fileName, string method)
        {
            var uri = string.Format("ftp://{0}/{1}/{2}", FtpHostName, directory, fileName);
            var ftpWebRequest = (FtpWebRequest)WebRequest.Create(uri);
            ftpWebRequest.Method = method;
            ftpWebRequest.UseBinary = true;
            ftpWebRequest.KeepAlive = true;
            ftpWebRequest.ConnectionGroupName = "BulkFtpCopyManager";
            ftpWebRequest.Credentials = new NetworkCredential(FtpUserName, FtpPasssword);
            return ftpWebRequest;
        }
    }
}
