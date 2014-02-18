using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TplTests
{
    [TestFixture]
    internal class CancellationTests
    {
        [Test]
        public void Test1()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var task = new Task(s =>
                {
                    var cancellationToken = (CancellationToken)s;
                    foreach (var _ in Enumerable.Range(1, 5))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Thread.Sleep(1 * 1000);
                    }
                }, cancellationTokenSource.Token, cancellationTokenSource.Token);
            Assert.That(task.Status, Is.EqualTo(TaskStatus.Created));
            task.Start();
            Assert.That(task.Status, Is.EqualTo(TaskStatus.Running).Or.EqualTo(TaskStatus.WaitingToRun));
            cancellationTokenSource.Cancel();

            var nonCancellationExceptionOccurred = false;

            try
            {
                task.Wait();
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    if (!(ex is TaskCanceledException))
                    {
                        nonCancellationExceptionOccurred = true;
                    }
                }
            }

            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.IsCanceled, Is.True);
            Assert.That(task.Status, Is.EqualTo(TaskStatus.Canceled));
            Assert.That(nonCancellationExceptionOccurred, Is.False);
        }

        [Test]
        public void Test2()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var task = new Task(() => Thread.Sleep(2 * 1000));
            var continuation = task.ContinueWith(_ => { }, cancellationTokenSource.Token);
            task.Start();

            cancellationTokenSource.Cancel();

            try
            {
                Task.WaitAll(task, continuation);
            }
            catch (AggregateException)
            {
            }

            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.IsCanceled, Is.False);
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));

            Assert.That(continuation.IsCompleted, Is.True);
            Assert.That(continuation.IsCanceled, Is.True);
            Assert.That(continuation.Status, Is.EqualTo(TaskStatus.Canceled));
        }
    }
}
