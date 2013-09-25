using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TplTests
{
    [TestFixture]
    internal class FirstSteps
    {
        [Test]
        public void CreateVoidTaskAndStartItAndWaitForIt()
        {
            var task = new Task(() => { });
            Assert.That(task.Status, Is.EqualTo(TaskStatus.Created));
            task.Start();
            task.Wait();
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
        }

        [Test]
        public void StartNewVoidTaskViaFactoryAndWaitForIt()
        {
            var task = Task.Factory.StartNew(() => { });
            task.Wait();
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
        }

        [Test]
        public void CreateTaskThatReturnsSomethingAndStartItAndWaitForIt()
        {
            var task = new Task<int>(() => 123);
            Assert.That(task.Status, Is.EqualTo(TaskStatus.Created));
            task.Start();
            task.Wait();
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(task.Result, Is.EqualTo(123));
        }

        [Test]
        public void StartNewTaskThatReturnsSomethingViaFactoryAndWaitForIt()
        {
            var task = Task<int>.Factory.StartNew(() => 456);
            task.Wait();
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(task.Result, Is.EqualTo(456));
        }

        [Test]
        public void StartTwoTasksThatReturnSomethingAndWaitForThem()
        {
            var task1 = Task<int>.Factory.StartNew(() => 12);
            var task2 = Task<int>.Factory.StartNew(() => 13);
            Task.WaitAll(task1, task2);
            var actual = task1.Result + task2.Result;
            Assert.That(actual, Is.EqualTo(12 + 13));
        }

        [Test]
        public void RunningTasksInParallelAndCombiningTheResults()
        {
            var enumerable = Enumerable.Range(1, 10);
            var tasks = new List<Task<IList<string>>>();

            // ReSharper disable LoopCanBeConvertedToQuery
            foreach (var num in enumerable)
            {
                var copyOfNum = num;
                var task = Task<IList<string>>.Factory.StartNew(() => MakeStrings(copyOfNum));
                tasks.Add(task);
            }
            // ReSharper restore LoopCanBeConvertedToQuery

            Task.WaitAll(tasks.Cast<Task>().ToArray());

            var combinedResults = tasks.SelectMany(t => t.Result);
            Assert.That(combinedResults.Count(), Is.EqualTo(55));
        }

        private static IList<string> MakeStrings(int numStrings)
        {
            var result = new List<string>();

            for (var i = 0; i < numStrings; i++)
            {
                result.Add(new string('*', numStrings));
            }

            return result;
        }

        // TODO: create a few tasks that return lists of things and combine the results
    }
}
