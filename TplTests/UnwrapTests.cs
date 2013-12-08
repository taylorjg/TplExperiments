using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TplTests
{
    [TestFixture]
    internal class UnwrapTests
    {
        [Test]
        public void Unwrap1()
        {
            var continuation = WebRequestAsync("http://bbc.co.uk")
                .ContinueWith(_ => WebRequestAsync("http://google.com"))
                .Unwrap();

            continuation.Wait();

            var title = ExtractTitle(continuation.Result);
            Assert.That(title, Is.StringContaining("Google"));
        }

        [Test]
        public void Unwrap2()
        {
            var continuation = WebRequestAsync("http://bbc.co.uk")
                .ContinueWith(t1 =>
                    {
                        var title1 = ExtractTitle(t1.Result);
                        return WebRequestAsync("http://google.com")
                            .ContinueWith(t2 =>
                                {
                                    var title2 = ExtractTitle(t2.Result);
                                    return Tuple.Create(title1, title2);
                                });
                    })
                .Unwrap();

            continuation.Wait();

            var tuple = continuation.Result;
            Assert.That(tuple.Item1, Is.StringContaining("BBC"));
            Assert.That(tuple.Item2, Is.StringContaining("Google"));
        }

        private static Task<WebResponse> WebRequestAsync(string url)
        {
            var webRequest = WebRequest.Create(url);
            return Task<WebResponse>.Factory.FromAsync(webRequest.BeginGetResponse, webRequest.EndGetResponse, null);
        }

        private static string ExtractTitle(WebResponse webResponse)
        {
            var responseStream = webResponse.GetResponseStream();

            if (responseStream != null)
            {
                var streamReader = new StreamReader(responseStream);
                var content = streamReader.ReadToEnd();

                var regex = new Regex(@"<head>.*<title>(.*)</title>");
                var match = regex.Match(content);

                if (match.Success)
                {
                    if (match.Groups.Count == 2)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }

            return string.Empty;
        }
    }
}
