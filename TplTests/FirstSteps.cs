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

        // TODO: create a few tasks that return lists of things and combine the results
    }
}
