using System;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TplTests
{
    // ReSharper disable InconsistentNaming

    public enum ServerState
    {
        Unknown = 0,
        BigLoopOnly = 1,
        LittleLoopOnly = 2,
        BothLoops = 3,
        NoLoops = 4
    }

    internal class ServerStateChecker
    {
        private const string BigLoopFileName = "ntt-lbwa-healthcheck.gif";
        private const string LittleLoopFileName = "drl_deploy_hc.gif";

        public ServerStateChecker(string serverIpAddress, string hostHeader, bool bigLoopOnly)
        {
            _serverIpAddress = serverIpAddress;
            _hostHeader = hostHeader;
            _bigLoopOnly = bigLoopOnly;
        }

        public Task<Tuple<ServerState, string>> Task()
        {
            var loopFileChecker1 = new LoopFileChecker(_serverIpAddress, _hostHeader, BigLoopFileName);
            var loopFileChecker2 = (_bigLoopOnly) ? new LoopFileChecker() : new LoopFileChecker(_serverIpAddress, _hostHeader, LittleLoopFileName);

            var task1 = loopFileChecker1.Task();
            var task2 = loopFileChecker2.Task();

            var continuation = System.Threading.Tasks.Task.WhenAll(task1, task2).ContinueWith(t =>
                {
                    var result1 = t.Result[0];
                    var result2 = t.Result[1];

                    if (!result1.Item1.HasValue || !result2.Item1.HasValue)
                    {
                        var exceptionMessage = result1.Item2 ?? result2.Item2;
                        return Tuple.Create(ServerState.Unknown, exceptionMessage);
                    }

                    var serverState = ServerState.Unknown;

                    var flags = Tuple.Create(result1.Item1.Value, result2.Item1.Value);

                    if (flags.Equals(Tuple.Create(true, true)))
                        serverState = ServerState.BothLoops;
                    else if (flags.Equals(Tuple.Create(false, false)))
                        serverState = ServerState.NoLoops;
                    else if (flags.Equals(Tuple.Create(true, false)))
                        serverState = ServerState.BigLoopOnly;
                    else if (flags.Equals(Tuple.Create(false, true)))
                        serverState = ServerState.LittleLoopOnly;

                    return Tuple.Create(serverState, null as string);
                });

            return continuation;
        }

        private readonly string _serverIpAddress;
        private readonly string _hostHeader;
        private readonly bool _bigLoopOnly;
    }

    internal class LoopFileChecker
    {
        public LoopFileChecker(string serverIpAddress, string hostHeader, string loopFileName)
        {
            _serverIpAddress = serverIpAddress;
            _hostHeader = hostHeader;
            _loopFileName = loopFileName;
            _skipLoopFileTest = false;
        }

        public LoopFileChecker()
        {
            _serverIpAddress = null;
            _hostHeader = null;
            _loopFileName = null;
            _skipLoopFileTest = true;
        }

        public Task<Tuple<bool?, string>> Task()
        {
            if (_skipLoopFileTest)
            {
                return System.Threading.Tasks.Task.Run(() => Tuple.Create(false as bool?, null as string));
            }

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(string.Format("http://{0}/DoNotDelete/{1}", _serverIpAddress, _loopFileName));
            httpWebRequest.Method = WebRequestMethods.Http.Head;
            httpWebRequest.Host = _hostHeader;
            var task = Task<WebResponse>.Factory.FromAsync(httpWebRequest.BeginGetResponse, httpWebRequest.EndGetResponse, null);
            var continuation = task.ContinueWith(t =>
                {
                    bool? result = true;
                    string exceptionMessage = null;
                    if (t.IsFaulted && t.Exception != null && t.Exception.InnerException != null)
                    {
                        var notFound = false;
                        var webResponse = ((WebException) t.Exception.InnerException).Response;
                        if (webResponse != null)
                        {
                            var httpWebResponse = (HttpWebResponse)webResponse;
                            notFound = (httpWebResponse.StatusCode == HttpStatusCode.NotFound);
                        }
                        if (notFound)
                        {
                            result = false;
                        }
                        else
                        {
                            result = null;
                            exceptionMessage = t.Exception.InnerException.Message;
                        }
                    }
                    return Tuple.Create(result, exceptionMessage);
                });
            return continuation;
        }

        private readonly string _serverIpAddress;
        private readonly string _hostHeader;
        private readonly string _loopFileName;
        private readonly bool _skipLoopFileTest;
    }

    [TestFixture]
    internal class ContinuationTests
    {
        private const string BigLoopFileName = "ntt-lbwa-healthcheck.gif";
        private const string LittleLoopFileName = "drl_deploy_hc.gif";
        private const string ServerInBigLoopOnly = "82.112.124.97";
        private const string ServerInLittleLoopOnly = "82.112.124.107";
        private const string BogusServer = "bogusxxxyyy";
        private const string HostHeader = "ao.com";

        [SetUp]
        public void SetUp()
        {
            ServicePointManager.DefaultConnectionLimit = 100;
        }

        [Test]
        public void Test1()
        {
            var loopFileChecker1 = new LoopFileChecker(ServerInBigLoopOnly, HostHeader, BigLoopFileName);
            var loopFileChecker2 = new LoopFileChecker(ServerInBigLoopOnly, HostHeader, LittleLoopFileName);
            var loopFileChecker3 = new LoopFileChecker(BogusServer, HostHeader, BigLoopFileName);
            var loopFileChecker4 = new LoopFileChecker();

            var task1 = loopFileChecker1.Task();
            var task2 = loopFileChecker2.Task();
            var task3 = loopFileChecker3.Task();
            var task4 = loopFileChecker4.Task();

            try
            {
                var tasks = new Task[] { task1, task2, task3, task4 };
                Task.WaitAll(tasks);
            }
            catch (AggregateException ae)
            {
                ae.Handle(ex => ex is WebException);
            }

            var result1 = task1.Result;
            var result2 = task2.Result;
            var result3 = task3.Result;
            var result4 = task4.Result;

            Assert.That(result1.Item1, Is.True);
            Assert.That(result1.Item2, Is.Null);

            Assert.That(result2.Item1, Is.False);
            Assert.That(result2.Item2, Is.Null);

            Assert.That(result3.Item1, Is.Null);
            Assert.That(result3.Item2, Is.StringStarting("The remote name could not be resolved"));

            Assert.That(result4.Item1, Is.False);
            Assert.That(result4.Item2, Is.Null);
        }

        [Test]
        public void ServerStateChecker_ServerInBigLoopOnly_ReturnsServerStateBigLoopOnly()
        {
            var serverStateChecker = new ServerStateChecker(ServerInBigLoopOnly, HostHeader, false);
            var t = serverStateChecker.Task();
            t.Wait();
            var result = t.Result;
            Assert.That(result.Item1, Is.EqualTo(ServerState.BigLoopOnly));
            Assert.That(result.Item2, Is.Null);
        }

        [Test]
        public void ServerStateChecker_ServerInLittleLoopOnly_ReturnsServerStateLittleLoopOnly()
        {
            var serverStateChecker = new ServerStateChecker(ServerInLittleLoopOnly, HostHeader, false);
            var t = serverStateChecker.Task();
            t.Wait();
            var result = t.Result;
            Assert.That(result.Item1, Is.EqualTo(ServerState.LittleLoopOnly));
            Assert.That(result.Item2, Is.Null);
        }

        [Test]
        public void ServerStateChecker_BogusServer_ReturnsServerStateUnknownAndExceptionMessage()
        {
            var serverStateChecker = new ServerStateChecker(BogusServer, HostHeader, false);
            var t = serverStateChecker.Task();
            t.Wait();
            var result = t.Result;
            Assert.That(result.Item1, Is.EqualTo(ServerState.Unknown));
            Assert.That(result.Item2, Is.StringStarting("The remote name could not be resolved"));
        }

        // TODO: add a couple more tests re bigLoopOnly: true
    }
}
