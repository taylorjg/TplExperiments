using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TplTests
{
    [TestFixture]
    internal class AsyncAwaitTests
    {
        [SetUp]
        public void SetUp()
        {
            ServicePointManager.DefaultConnectionLimit = 100;
        }

        [Test]
        public async void SingleWebRequestUsingAsyncAwait()
        {
            var task = WebRequest.Create("http://ao.com").GetResponseAsync();
            var webResponse = await task;
            var httpWebResponse = (HttpWebResponse)webResponse;
            Assert.That(httpWebResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async void MultipleWebRequestsUsingAsyncAwait()
        {
            // Arrange
            var tasks = Enumerable.Range(0, 5).Select(_ => WebRequest.Create("http://google.com").GetResponseAsync());

            // Act
            var webResponses = await Task.WhenAll(tasks);

            // Assert
            foreach (var httpWebResponse in webResponses.Cast<HttpWebResponse>())
            {
                Assert.That(httpWebResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            }
        }

        [Test]
        public async void MultipleWebRequestsIncludingOneThatWillFailUsingAsyncAwait()
        {
            // Arrange
            var urls = new[]
                {
                    "http://ao.com",
                    "http://bbc.co.uk/banana",
                    "http://ao.com",
                    "http://ao.com",
                    "http://ao.com"
                };

            var tasks = urls.Select(url => WebRequest.Create(url).GetResponseAsync()).ToList();

            // Act
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (WebException)
            {
            }

            // Assert
            AssertTaskStatusAndHttpStatusCode(tasks, 0, TaskStatus.RanToCompletion, HttpStatusCode.OK);
            AssertTaskStatusAndHttpStatusCode(tasks, 2, TaskStatus.RanToCompletion, HttpStatusCode.OK);
            AssertTaskStatusAndHttpStatusCode(tasks, 3, TaskStatus.RanToCompletion, HttpStatusCode.OK);
            AssertTaskStatusAndHttpStatusCode(tasks, 4, TaskStatus.RanToCompletion, HttpStatusCode.OK);

            var faultedTask = tasks[1];
            Assert.That(faultedTask.Status, Is.EqualTo(TaskStatus.Faulted));
            // ReSharper disable PossibleNullReferenceException
            Assert.That(faultedTask.Exception, Is.Not.Null);
            Assert.That(faultedTask.Exception.InnerException, Is.Not.Null);
            Assert.That(faultedTask.Exception.InnerException, Is.InstanceOf<WebException>());
            // ReSharper restore PossibleNullReferenceException
            var webResponse = (HttpWebResponse)((WebException)faultedTask.Exception.InnerException).Response;
            Assert.That(webResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        private static void AssertTaskStatusAndHttpStatusCode(IList<Task<WebResponse>> tasks, int taskIndex, TaskStatus taskStatus, HttpStatusCode httpStatusCode)
        {
            Assert.That(tasks[taskIndex].Status, Is.EqualTo(taskStatus));
            var webResponse = tasks[taskIndex].Result;
            var httpWebResponse = (HttpWebResponse)webResponse;
            Assert.That(httpWebResponse.StatusCode, Is.EqualTo(httpStatusCode));
        }
    }
}
