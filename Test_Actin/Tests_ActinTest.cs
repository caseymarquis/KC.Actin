using KC.Actin;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin
{
    public class Tests_ActinTest
    {
        public class Parent : Actor {
            [Singleton] public ParentAndChild ParentAndChild_Parent;
            [Singleton] public ParentOnly ParentOnly;

            protected override Task OnRun(ActorUtil util) {
                return Task.FromResult(0);
            }
        }

        [Singleton]
        public class Child : Parent {
            [Singleton] public ParentAndChild ParentAndChild_Child;
            [Singleton] public ChildOnly ChildOnly;
        }

        [Singleton] public class ParentOnly { }
        [Singleton] public class ChildOnly { }
        [Singleton] public class ParentAndChild { }

        [Fact]
        public async Task GetDependency()
        {
            var at = new ActinTest();
            var child = await at.GetInitializedActor<Child>();
            Assert.NotNull(child);
            var parentOnly = at.GetDependency<ParentOnly>(child);
            Assert.Equal(child.ParentOnly, parentOnly);
            var childOnly = at.GetDependency<ChildOnly>(child);
            Assert.Equal(child.ChildOnly, childOnly);
            var parentAndChild_child = at.GetDependency<ParentAndChild>(child);
            Assert.Equal(child.ParentAndChild_Child, parentAndChild_child);
        }
    }
}
