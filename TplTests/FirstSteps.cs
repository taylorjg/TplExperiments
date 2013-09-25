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

        // TODO: create a few tasks that return lists of things and combine the results
    }
}
