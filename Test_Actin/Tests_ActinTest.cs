using KC.Actin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin {
    public class Tests_ActinTest {
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

        [Singleton]
        public class SceneParent : Scene<SceneChild> {
            protected override async Task<IEnumerable<Role>> CastActors(ActorUtil util, Dictionary<int, SceneChild> myActors) {
                return new Role[] {
                    new Role{ Id = 1, },
                    new Role{ Id = 2, },
                    new Role{ Id = 3, },
                };
            }
        }

        [Instance]
        public class SceneChild : Actor {
            [FlexibleParent] public SceneParent Parent;
            [Singleton] public SceneChildDependencySingleton Singleton;
            [Instance] public SceneChildDependencySingleton Instance;
            protected override async Task OnRun(ActorUtil util) {
                await Task.FromResult(0);
            }
        }

        [Singleton]
        public class SceneChildDependencySingleton {
        }

        [Instance]
        public class SceneChildDependencyInstance {
        }

        [Singleton] public class ParentOnly { }
        [Singleton] public class ChildOnly { }
        [Singleton] public class ParentAndChild { }

        public class POCO : IPOCO { }
        public interface IPOCO { }

        [Fact]
        public async Task GetDependency() {
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

        [Fact]
        public void GetDirector() {
            var at = new ActinTest();
            Assert.NotNull(at.Director);
            var director = at.GetObject<Director>();
            Assert.NotNull(director);
            Assert.Same(director.Clock, at.Clock);
            Assert.Same(director, at.Director);
        }

        [Fact]
        public void SetAndGetSingleton() {
            var at = new ActinTest();

            var poco = new POCO();
            at.AddObject(poco, typeof(IPOCO));
            var pocoDirect = at.GetObject<POCO>();
            var pocoAlias = at.GetObject<IPOCO>();
            Assert.Same(poco, pocoDirect);
            Assert.Same(poco, pocoAlias);
        }

        [Fact]
        public async Task GenerateSceneChildren() {
            var at = new ActinTest();
            var scene = at.GetActor<SceneParent>();
            Assert.NotNull(scene);
            await at.RunActor(scene);
            var allChildren = scene.Actors_GetAll();
            Assert.Contains(allChildren, x => x.Id == 1);
            Assert.Contains(allChildren, x => x.Id == 2);
            Assert.Contains(allChildren, x => x.Id == 3);

            var child = allChildren.First();
            Assert.NotNull(child.Singleton);
            Assert.NotNull(child.Instance);
            Assert.NotNull(child.Parent);

            Assert.Equal(scene, child.Parent);
        }
    }
}
