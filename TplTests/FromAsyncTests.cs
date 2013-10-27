﻿using System;
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
            ServicePointManager.DefaultConnectionLimit = 10;
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

            Assert.That(tasks[0].Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(((HttpWebResponse)tasks[0].Result).StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(tasks[1].Status, Is.EqualTo(TaskStatus.Faulted));
            // ReSharper disable PossibleNullReferenceException
            Assert.That(tasks[1].Exception, Is.Not.Null);
            Assert.That(tasks[1].Exception.InnerException, Is.Not.Null);
            Assert.That(tasks[1].Exception.InnerException, Is.InstanceOf<WebException>());
            // ReSharper restore PossibleNullReferenceException
            var webResponse = (HttpWebResponse)((WebException)tasks[1].Exception.InnerException).Response;
            Assert.That(webResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            Assert.That(tasks[2].Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(((HttpWebResponse)tasks[0].Result).StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(tasks[3].Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(((HttpWebResponse)tasks[0].Result).StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(tasks[4].Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(((HttpWebResponse)tasks[0].Result).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }
}
