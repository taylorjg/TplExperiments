using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TplTests
{
    [TestFixture]
    internal class FromAsyncTests
    {
        [SetUp]
        public void SetUp()
        {
            ServicePointManager.DefaultConnectionLimit = 100;
        }

        [Test]
        public void SingleWebRequest()
        {
            var webRequest = WebRequest.Create("http://ao.com");
            var task = Task<WebResponse>.Factory.FromAsync(webRequest.BeginGetResponse, webRequest.EndGetResponse, null);
            task.Wait();
            var webResponse = (HttpWebResponse)task.Result;
            Assert.That(webResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public void MultipleWebRequests()
        {
            var taskFactory = Task<WebResponse>.Factory;

            var tasks = Enumerable.Range(0, 5)
                .Select(_ => WebRequest.Create("http://google.com"))
                .Select(webRequest => taskFactory.FromAsync(webRequest.BeginGetResponse, webRequest.EndGetResponse, null)).ToList();

            Task.WaitAll(tasks.Cast<Task>().ToArray());

            foreach (var webResponse in tasks.Select(task => task.Result))
            {
                Assert.That(((HttpWebResponse)webResponse).StatusCode, Is.EqualTo(HttpStatusCode.OK));
            }
        }

        [Test]
        public void MultipleWebRequestsIncludingOneThatWillFail()
        {
            var taskFactory = Task<WebResponse>.Factory;

            var tasks = Enumerable.Range(0, 5)
                .Select(index =>
                    {
                        var url = (index == 1) ? "http://bbc.co.uk/banana" : "http://ao.com";
                        return WebRequest.Create(url);
                    })
                .Select(webRequest => taskFactory.FromAsync(webRequest.BeginGetResponse, webRequest.EndGetResponse, null)).ToList();

            try
            {
                Task.WaitAll(tasks.Cast<Task>().ToArray());
            }
            catch (AggregateException ae)
            {
                ae.Handle(ex => ex is WebException);
            }

            AssertTaskStatusAndHttpStatusCode(tasks, 0, TaskStatus.RanToCompletion, HttpStatusCode.OK);
            AssertTaskStatusAndHttpStatusCode(tasks, 2, TaskStatus.RanToCompletion, HttpStatusCode.OK);
            AssertTaskStatusAndHttpStatusCode(tasks, 3, TaskStatus.RanToCompletion, HttpStatusCode.OK);
            AssertTaskStatusAndHttpStatusCode(tasks, 4, TaskStatus.RanToCompletion, HttpStatusCode.OK);

            Assert.That(tasks[1].Status, Is.EqualTo(TaskStatus.Faulted));
            // ReSharper disable PossibleNullReferenceException
            Assert.That(tasks[1].Exception, Is.Not.Null);
            Assert.That(tasks[1].Exception.InnerException, Is.Not.Null);
            Assert.That(tasks[1].Exception.InnerException, Is.InstanceOf<WebException>());
            // ReSharper restore PossibleNullReferenceException
            var webResponse = (HttpWebResponse)((WebException)tasks[1].Exception.InnerException).Response;
            Assert.That(webResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async void SingleWebRequestUsingAsyncAwait()
        {
            var task = WebRequest.Create("http://ao.com").GetResponseAsync();
            var webResponse = await task;
            var httpWebResponse = (HttpWebResponse) webResponse;
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
            var httpWebResponse = (HttpWebResponse) webResponse;
            Assert.That(httpWebResponse.StatusCode, Is.EqualTo(httpStatusCode));
        }
    }
}
