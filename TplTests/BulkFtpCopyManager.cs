using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace TplTests
{
    internal class BulkFtpCopyManager
    {
        private readonly string _ftpUserName;
        private readonly string _ftpPassword;

        public BulkFtpCopyManager(string ftpUserName, string ftpPassword)
        {
            _ftpUserName = ftpUserName;
            _ftpPassword = ftpPassword;
        }

        private class WareHouse
        {
            public WareHouse(string ftpUserName, string ftpPassword, string sourceBaseUri)
            {
                _ftpUserName = ftpUserName;
                _ftpPassword = ftpPassword;
                _sourceBaseUri = sourceBaseUri;
            }

            public void EnqueueCopyOperation(string targetBaseUri, string fileName)
            {
                WareHouseData wareHouseData;
                if (!_dictionary.TryGetValue(fileName, out wareHouseData))
                {
                    wareHouseData = new WareHouseData(_ftpUserName, _ftpPassword);
                    _dictionary.Add(fileName, wareHouseData);
                    var startingTask = wareHouseData.CreateStartingTask(_sourceBaseUri, fileName);
                    _startingTasks.Add(startingTask);
                }

                var finalTask = wareHouseData.EnqueueCopyOperation(targetBaseUri);
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
                    public FileDownloadState(Func<string, string, string, FtpWebRequest> ftpWebRequestFactoryFunc, string sourceBaseUri, string fileName)
                    {
                        FtpWebRequestFactoryFunc = ftpWebRequestFactoryFunc;
                        SourceBaseUri = sourceBaseUri;
                        FileName = fileName;
                    }

                    public string FileName { get; private set; }
                    public Byte[] Buffer { get; private set; }

                    private Func<string, string, string, FtpWebRequest> FtpWebRequestFactoryFunc { get; set; }
                    private string SourceBaseUri { get; set; }
                    private FtpWebRequest DownloadFtpWebRequest { get; set; }
                    private FtpWebResponse DownloadFtpWebResponse { get; set; }
                    private Stream DownloadResponseStream { get; set; }
                    private MemoryStream DownloadDestinationStream { get; set; }
                    private Stream WrappedDownloadDestinationStream { get; set; }

                    public Task<WebResponse> StartFtpDownload()
                    {
                        MyDebug.Log("StartFtpDownload - SourceDirectory: {0}; FileName: {1}", SourceBaseUri, FileName);
                        DownloadFtpWebRequest = FtpWebRequestFactoryFunc(SourceBaseUri, FileName, WebRequestMethods.Ftp.DownloadFile);
                        return DownloadFtpWebRequest.GetResponseAsync();
                    }

                    public Task ReadDownloadResponseStream(FtpWebResponse ftpWebResponse)
                    {
                        MyDebug.Log("ReadDownloadResponseStream - SourceDirectory: {0}; FileName: {1}", SourceBaseUri, FileName);
                        DownloadFtpWebResponse = ftpWebResponse;
                        DownloadResponseStream = DownloadFtpWebResponse.GetResponseStream();
                        DownloadDestinationStream = new MemoryStream();
                        WrappedDownloadDestinationStream = new StreamWrapper(DownloadDestinationStream, SourceBaseUri, FileName);
                        // ReSharper disable PossibleNullReferenceException
                        return DownloadResponseStream.CopyToAsync(WrappedDownloadDestinationStream);
                        // ReSharper restore PossibleNullReferenceException
                    }

                    public void Cleanup()
                    {
                        MyDebug.Log("Cleanup - SourceDirectory: {0}; FileName: {1}", SourceBaseUri, FileName);
                        WrappedDownloadDestinationStream.Close();
                        Buffer = DownloadDestinationStream.ToArray();
                        DownloadFtpWebResponse.Close();
                    }
                }

                private class FileUploadState
                {
                    public FileUploadState(Func<string, string, string, FtpWebRequest> ftpWebRequestFactoryFunc, FileDownloadState fileDownloadState, string targetBaseUri)
                    {
                        FtpWebRequestFactoryFunc = ftpWebRequestFactoryFunc;
                        FileDownloadState = fileDownloadState;
                        TargetBaseUri = targetBaseUri;
                    }

                    private Func<string, string, string, FtpWebRequest> FtpWebRequestFactoryFunc { get; set; }
                    private FileDownloadState FileDownloadState { get; set; }
                    private string TargetBaseUri { get; set; }
                    private FtpWebRequest UploadFtpWebRequest { get; set; }
                    private FtpWebResponse UploadFtpWebResponse { get; set; }
                    private Stream WrappedUploadRequestStream { get; set; }
                    private Stream UploadSourceStream { get; set; }

                    public Task<Stream> StartFtpUpload()
                    {
                        MyDebug.Log("StartFtpUpload - TargetDirectory: {0}; FileName: {1}", TargetBaseUri, FileDownloadState.FileName);
                        UploadFtpWebRequest = FtpWebRequestFactoryFunc(TargetBaseUri, FileDownloadState.FileName, WebRequestMethods.Ftp.UploadFile);
                        return UploadFtpWebRequest.GetRequestStreamAsync();
                    }

                    public Task WriteUploadRequestStream(Stream uploadRequestStream)
                    {
                        MyDebug.Log("WriteUploadRequestStream - TargetDirectory: {0}; FileName: {1}", TargetBaseUri, FileDownloadState.FileName);
                        WrappedUploadRequestStream = new StreamWrapper(uploadRequestStream, TargetBaseUri, FileDownloadState.FileName);
                        UploadSourceStream = new MemoryStream(FileDownloadState.Buffer);
                        return UploadSourceStream.CopyToAsync(WrappedUploadRequestStream);
                    }

                    public Task<WebResponse> FinishFtpUpload()
                    {
                        MyDebug.Log("FinishFtpUpload - TargetDirectory: {0}; FileName: {1}", TargetBaseUri, FileDownloadState.FileName);
                        return UploadFtpWebRequest.GetResponseAsync();
                    }

                    public void Cleanup(FtpWebResponse ftpWebResponse)
                    {
                        MyDebug.Log("Cleanup - TargetDirectory: {0}; FileName: {1}", TargetBaseUri, FileDownloadState.FileName);
                        WrappedUploadRequestStream.Close();
                        UploadSourceStream.Close();
                        UploadFtpWebResponse = ftpWebResponse;
                        UploadFtpWebResponse.Close();
                    }
                }

                public WareHouseData(string ftpUserName, string ftpPassword)
                {
                    _ftpUserName = ftpUserName;
                    _ftpPassword = ftpPassword;
                }

                public Task CreateStartingTask(string sourceDirectory, string fileName)
                {
                    _startingTask = new Task(NoOperation);

                    _fileDownloadState = new FileDownloadState(CreateFtpWebRequest, sourceDirectory, fileName);

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

                public Task EnqueueCopyOperation(string targetBaseUri)
                {
                    var fileUploadState = new FileUploadState(CreateFtpWebRequest, _fileDownloadState, targetBaseUri);

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

                private FtpWebRequest CreateFtpWebRequest(string baseUri, string fileName, string method)
                {
                    var uri = string.Format("{0}/{1}", baseUri, fileName);
                    var ftpWebRequest = (FtpWebRequest)WebRequest.Create(uri);
                    ftpWebRequest.Method = method;
                    ftpWebRequest.UseBinary = true;
                    ftpWebRequest.KeepAlive = true;
                    ftpWebRequest.ConnectionGroupName = "BulkFtpCopyManager";
                    ftpWebRequest.Credentials = new NetworkCredential(_ftpUserName, _ftpPassword);
                    return ftpWebRequest;
                }

                private readonly string _ftpUserName;
                private readonly string _ftpPassword;
                private Task _startingTask;
                private FileDownloadState _fileDownloadState;
                private Task _lastDownloadTask;
            }

            private readonly string _ftpUserName;
            private readonly string _ftpPassword;
            private readonly string _sourceBaseUri;
            private readonly IDictionary<string, WareHouseData> _dictionary = new Dictionary<string, WareHouseData>();
            private readonly IList<Task> _startingTasks = new List<Task>();
            private readonly IList<Task> _finalTasks = new List<Task>();
        }

        public async Task CopyFiles(IList<string> fileNames, string sourceBaseUri, IList<string> targetBaseUris)
        {
            MyDebug.Log("Entering CopyFiles");

            var wareHouse = new WareHouse(_ftpUserName, _ftpPassword, sourceBaseUri);

            foreach (var targetBaseUri in targetBaseUris)
            {
                foreach (var fileName in fileNames)
                {
                    wareHouse.EnqueueCopyOperation(targetBaseUri, fileName);
                }
            }

            MyDebug.Log("Calling wareHouse.StartCopyOperations");
            wareHouse.StartCopyOperations();

            MyDebug.Log("Calling wareHouse.WaitForCopyOperationsToComplete");
            await wareHouse.WaitForCopyOperationsToComplete();

            MyDebug.Log("Leaving CopyFiles");
        }
    }
}
