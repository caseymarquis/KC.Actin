using KC.Actin;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin
{
    public class Tests_CircularDependencyDetection
    {
        [Instance]
        public class ClassA {
            [Instance]
            private ClassB B;
        }

        public class ClassB {
            [Instance]
            private ClassC C;
        }

        public class ClassC {
            [Instance]
            private ClassA A;
        }

        [Fact]
        public async Task ThrowOnCircularDependency()
        {
            var director = new Director();
            await Assert.ThrowsAsync<ApplicationException>(async () => {
                await director.Run(configure: async (util) => {
                    var nestedTypes = typeof(Tests_CircularDependencyDetection).GetNestedTypes();
                    util.Set_RootActorFilter(x => nestedTypes.Contains(x.Type));
                    util.Set_AssembliesToCheckForDependencies(Assembly.GetExecutingAssembly());
                    await Task.FromResult(0);
                });
            });
        }
    }
}
