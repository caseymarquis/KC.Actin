using KC.Ricochet;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KC.Actin {
    public class ActinInstantiator {
        public readonly bool IsRootSingleton;
        public readonly Type Type;
        private bool runBefore;
        //Null unless a Parent or Sibling attribute was used. To keep it null, use the flexible attributes instead.
        private Type StaticParentType;
        private object providedSingletonInstance;

        Instantiator RicochetInstantiator;
        List<AccessorInstantiatorPair> SingletonDependencies = new List<AccessorInstantiatorPair>();
        List<AccessorInstantiatorPair> InstanceDependencies = new List<AccessorInstantiatorPair>();
        List<AccessorInstantiatorPair> ParentDependencies = new List<AccessorInstantiatorPair>();
        List<AccessorInstantiatorPair> SiblingDependencies = new List<AccessorInstantiatorPair>();

        public ActinInstantiator(Type t, object _providedSingletonInstance = null) {
            providedSingletonInstance = _providedSingletonInstance;
            Type = t;
            IsRootSingleton = providedSingletonInstance != null || t.HasAttribute<SingletonAttribute>();

            if (_providedSingletonInstance == null) {
                try {
                    RicochetInstantiator = Ricochet.Util.GetConstructor(Type);
                }
                catch (ApplicationException ex) {
                    throw new ApplicationException($"{Type.Name} has no parameterless public constructor.", ex);
                }
            }
        }

        internal void Build(Func<Type, ActinInstantiator> getInstantiatorFromType, ImmutableList<ActinInstantiator> lineage = null) {
            //Check for circular dependencies:
            if (lineage != null) {
                if (lineage.Any(x => x.Type == this.Type)) {
                    var sb = new StringBuilder();
                    sb.AppendLine("Circular dependency detected: ");
                    foreach (var inst in lineage) {
                        sb.Append("|--");
                        sb.Append(inst.Type.Name);
                        if (inst.Type == this.Type) {
                            sb.Append(" <== Declared Here");
                        }
                        sb.AppendLine();
                    }
                    throw new ApplicationException(sb.ToString());
                }
            }

            var parentType = lineage?.Last().Type;
            if (StaticParentType != null && StaticParentType != parentType) {
                //If run from two different types, some of the build type checks won't be run a second time.
                //To ensure we catch type errors on startup, we check if the cached parent type matches the stored parent type.
                throw new ApplicationException($"{this.Type.Name} has a parent dependency of type {parentType.Name}, but this conflicts with a previous parent dependency {StaticParentType.Name}. Use the FlexibleParent and FlexibleSibling attributes if {this.Type.Name} only sometimes has a parent/sibling available, or if the type of a parent/sibling may change.");
            }

            if (!runBefore) {
                var allAccessors = Ricochet.Util.GetPropsAndFields(this.Type, x => x.IsClass);
                foreach (var memberAccessor in allAccessors) {
                    AccessorInstantiatorPair getPair() =>
                        new AccessorInstantiatorPair {
                            Accessor = memberAccessor,
                            Instantiator = getInstantiatorFromType(memberAccessor.Type),
                        };

                    if (memberAccessor.Markers.Contains(nameof(SingletonAttribute))) {
                        this.SingletonDependencies.Add(getPair());
                    }
                    else if (memberAccessor.Markers.Contains(nameof(InstanceAttribute))) {
                        this.InstanceDependencies.Add(getPair());
                    }
                    else if (memberAccessor.Markers.Contains(nameof(ParentAttribute))) {
                        var pair = getPair();
                        if (lineage == null) {
                            throw new ApplicationException($"{this.Type.Name} has a parent attribute, but is being used as a root dependency. Use the FlexibleParent attribute if {this.Type.Name} only sometimes has a parent available, or if the type of the parent may change.");
                        }
                        else if (parentType != pair.Instantiator.Type) {
                            throw new ApplicationException($"{this.Type.Name}.{pair.Accessor.Name} has a parent dependency of type {pair.Accessor.Type.Name}. Its actual parent is {parentType.Name}. Use the FlexibleParent attribute if {this.Type.Name} only sometimes has a parent available, or if the type of the parent may change.");
                        }
                        StaticParentType = parentType;
                        if (this.ParentDependencies.Any()) {
                            throw new ApplicationException($"{this.Type.Name} has more than one parent dependency. The first is {pair.Accessor.Name}. Use the FlexibleParent attribute if {this.Type.Name} only sometimes has a parent available, or if the type of the parent may change.");
                        }
                        this.ParentDependencies.Add(pair);
                    }
                    else if (memberAccessor.Markers.Contains(nameof(FlexibleParentAttribute))) {
                        this.ParentDependencies.Add(getPair());
                    }
                    else if (memberAccessor.Markers.Contains(nameof(SiblingAttribute))) {
                        var pair = getPair();
                        if (lineage == null) {
                            throw new ApplicationException($"{this.Type.Name} has a sibling attribute, but is being used as a root dependency. Use the FlexibleSibling attribute if {this.Type.Name} only sometimes has a parent available, or if the type of the parent may change.");
                        }
                        else {
                            StaticParentType = parentType;
                            var siblingDepPair = lineage.Last().InstanceDependencies.FirstOrDefault(x => x.Instantiator.Type == pair.Instantiator.Type);
                            if (siblingDepPair == null) {
                                throw new ApplicationException($"{this.Type.Name}.{pair.Accessor.Name} has a sibling dependency of type {pair.Accessor.Type.Name}. Its parent is {lineage.Last().Type.Name} does not have an Instance dependency which matches this. Use the FlexibleSibling attribute if {this.Type.Name} only sometimes has a parent available, or if the type of the parent may change.");
                            }
                            else {
                                pair.SiblingDependencyPair = siblingDepPair;
                            }
                        }
                        this.SiblingDependencies.Add(pair);
                    }
                    else if (memberAccessor.Markers.Contains(nameof(FlexibleSiblingAttribute))) {
                        this.SiblingDependencies.Add(getPair());
                    }
                }
                runBefore = true;
            }

            //We don't need to build sibling/parent dependencies, as they are just references to instance dependencies.
            //Singleton dependencies are all sent to this function without recursion,
            //and are also references, so we don't need to recursively build those dependencies here either.
            var newLineage = lineage?.Add(this) ?? ImmutableList.Create(this);
            foreach (var dep in InstanceDependencies) {
                dep.Instantiator.Build(getInstantiatorFromType, newLineage);
            }
        }

        private object CreateNew() {
            return RicochetInstantiator.New();
        }

        private void ResolveDependencies(object instance, DependencyType dependencyType, object parent, ActinInstantiator parentInstantiator, Director director) {
            var asActor = instance as Actor_SansType;
            if (asActor != null) {
                asActor.Instantiator = this; //Used for automatically disposing child dependencies.
            }
            Func<AccessorInstantiatorPair, bool> notSet = (x) => x.Accessor.GetVal(instance) == null;
            //Set and Resolve Singletons:
            foreach (var dep in SingletonDependencies.Where(notSet)) {
                dep.Accessor.SetVal(instance, dep.Instantiator.GetSingletonInstance(director));
            }
            //Set Child Instances:
            var unresolvedInstanceDependencies = new List<AccessorInstantiatorPairWithInstance>();
            foreach (var dep in InstanceDependencies.Where(notSet)) {
                var childInstance = dep.Instantiator.CreateNew();
                unresolvedInstanceDependencies.Add(new AccessorInstantiatorPairWithInstance {
                    Pair = dep,
                    Instance = childInstance,
                });
                dep.Accessor.SetVal(instance, childInstance);
            }
            //Resolve Child Instances:
            foreach (var dep in unresolvedInstanceDependencies) {
                dep.Pair.Instantiator.ResolveDependencies(dep.Instance, DependencyType.Instance, instance, this, director);
            }

            if (dependencyType == DependencyType.Instance && parent != null) {
                //Means we can have parents and siblings:
                foreach (var dep in ParentDependencies.Where(notSet)) {
                    try {
                        dep.Accessor.SetVal(instance, parent);
                    }
                    catch when (dep.Accessor.Markers.Contains(nameof(FlexibleParentAttribute))) {
                        //Swallow the exception, as FlexibleParents may be different types.
                    }
                    catch (Exception ex) {
                        throw new ApplicationException($"Actin failed to set {this.Type.Name}.{dep.Accessor.Name} with parent type {parentInstantiator?.Type.Name ?? "'Not Specified'"} and parent instance {parent ?? "null"}.", ex);
                    }
                }

                foreach (var dep in SiblingDependencies.Where(notSet)) {
                    if (dep.SiblingDependencyPair != null) {
                        //Normal Sibling:
                        try {
                            var siblingInstance = dep.SiblingDependencyPair.Accessor.GetVal(parent);
                            dep.Accessor.SetVal(instance, siblingInstance);
                        }
                        catch (Exception ex) {
                            throw new ApplicationException($"Actin failed to set sibling dependency {this.Type.Name}.{dep.Accessor.Name} when using parent type {parentInstantiator?.Type.Name ?? "'Not Specified'"} and parent instance {parent ?? "null"}.", ex);
                        }
                    }
                    else {
                        //Flexible Sibling:
                        foreach (var parentInstanceDep in parentInstantiator.InstanceDependencies) {
                            if (parentInstanceDep.Instantiator.Type.IsAssignableFrom(dep.Instantiator.Type)) {
                                var siblingInstance = parentInstanceDep.Accessor.GetVal(parent);
                                if (siblingInstance != null) {
                                    try {
                                        dep.Accessor.SetVal(instance, siblingInstance);
                                        break;
                                    }
                                    catch {
                                        //Swallow 
                                    }
                                }
                            }
                        }
                    }
                }

                //Add all child actors to the pool, as all of their dependencies have now been resolved.
                foreach (var dep in unresolvedInstanceDependencies.Where(x => x.Instance is Actor_SansType)) {
                    director?.AddActor((Actor_SansType)dep.Instance);
                }
            }
        }

        internal void DisposeChildren(object instance) {
            foreach (var dep in this.InstanceDependencies) {
                var childInstance = dep.Accessor.GetVal(instance);
                if (!object.Equals(null, childInstance)) {
                    List<Exception> exceptions = null;
                    try {
                        var asDisposable = childInstance as IDisposable;
                        asDisposable.Dispose();
                        if (childInstance is Actor_SansType) {
                            //Let it dispose of its own child dependencies.
                        }
                        else {
                            //Dispose of its child dependencies now:
                            dep.Instantiator?.DisposeChildren(childInstance);
                        }
                    }
                    catch (Exception ex) {
                        if (exceptions == null) {
                            exceptions = new List<Exception>();
                        }
                        exceptions.Add(ex);
                    }
                    if (exceptions != null) {
                        throw new AggregateException(exceptions);
                    }
                }
            }
        }

        private object lockSingleton = new object();
        private object singleton;

        public bool HasSingletonInstance => singleton != null;

        internal object GetSingletonInstance(Director director) {
            lock (lockSingleton) {
                if (singleton == null) {
                    singleton = providedSingletonInstance ?? CreateNew();
                    ResolveDependencies(singleton, DependencyType.Singleton, null, null, director);
                    if (singleton is Actor_SansType) {
                        director?.AddActor((Actor_SansType)singleton);
                    }
                }
                return singleton;
            }
        }

        internal object GetInstance(Director director, object parent, ActinInstantiator parentInstantiator = null) {
            var instance = CreateNew();
            ResolveDependencies(instance, DependencyType.Instance, parent, parentInstantiator, director);
            if (instance is Actor_SansType) {
                director?.AddActor((Actor_SansType)instance);
            }
            return instance;
        }

        class AccessorInstantiatorPair {
            public ActinInstantiator Instantiator;
            public PropertyAndFieldAccessor Accessor;
            //Used for get a sibling member from a parent:
            public AccessorInstantiatorPair SiblingDependencyPair;
        }

        class AccessorInstantiatorPairWithInstance {
            public AccessorInstantiatorPair Pair;
            public object Instance;
        }
    }

    public enum DependencyType {
        Singleton,
        Instance,
    }
}
