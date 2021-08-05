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
        public readonly bool IsActor;
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
            IsActor = typeof(Actor_SansType).IsAssignableFrom(t);

            if (_providedSingletonInstance == null) {
                try {
                    RicochetInstantiator = RicochetUtil.GetConstructor(Type);
                }
                catch (ApplicationException ex) {
                    throw new ApplicationException($"{Type.Name} has no parameterless public constructor.", ex);
                }
            }
        }

        /// <summary>
        /// Returning false means we refused to build the class because
        /// it has parents or siblings and therefore MUST have its lineage
        /// passed in.
        /// </summary>
        internal bool Build(Func<Type, ActinInstantiator> getInstantiatorFromType, ImmutableList<ActinInstantiator> lineage = null) {
            if (lineage == null) {
                var allAccessors = RicochetUtil.GetPropsAndFields(this.Type, x => x.IsClass);
                if (allAccessors.Any(x => x.Markers.Contains(nameof(ParentAttribute)) || x.Markers.Contains(nameof(SiblingAttribute)))) {
                    return false;
                }
            }
            //Check for circular dependencies:
            if (lineage != null) {
                if (lineage.Any(x => x.Type == this.Type)) {
                    var lineagePlusOne = lineage.Add(this);
                    var sb = new StringBuilder();
                    sb.AppendLine("Circular dependency detected: ");
                    int depth = 0;
                    foreach (var inst in lineagePlusOne) {
                        depth++;
                        sb.Append("|");
                        for (int i = 0; i < depth; i++) {
                            sb.Append("-");
                        }
                        sb.Append(inst.Type.Name);
                        if (inst.Type == this.Type) {
                            sb.Append(" <== Declared Here");
                        }
                        sb.AppendLine();
                    }
                    throw new ApplicationException(sb.ToString());
                }
            }

            var parentType = lineage?.Last().Type ?? StaticParentType;
            if (StaticParentType != null && StaticParentType != parentType) {
                //If run from two different types, some of the build type checks won't be run a second time.
                //To ensure we catch type errors on startup, we check if the cached parent type matches the stored parent type.
                throw new ApplicationException($"{this.Type.Name} has a parent dependency of type {parentType.Name}, but this conflicts with a previous parent dependency {StaticParentType.Name}. Use the FlexibleParent and FlexibleSibling attributes if {this.Type.Name} only sometimes has a parent/sibling available, or if the type of a parent/sibling may change.");
            }

            if (!runBefore) {
                var allAccessors = RicochetUtil.GetPropsAndFields(this.Type, x => x.IsClass);
                foreach (var memberAccessor in allAccessors) {
                    AccessorInstantiatorPair getPair(bool flexible) =>
                        new AccessorInstantiatorPair {
                            Accessor = memberAccessor,
                            Instantiator = flexible? null : getInstantiatorFromType(memberAccessor.Type),
                        };

                    if (memberAccessor.Markers.Contains(nameof(SingletonAttribute))) {
                        this.SingletonDependencies.Add(getPair(flexible: false));
                    }
                    else if (memberAccessor.Markers.Contains(nameof(InstanceAttribute))) {
                        this.InstanceDependencies.Add(getPair(flexible: false));
                    }
                    else if (memberAccessor.Markers.Contains(nameof(ParentAttribute))) {
                        var pair = getPair(flexible: false);
                        if (lineage == null) {
                            throw new ApplicationException($"{this.Type.Name} has a parent attribute, but is being used in a scene or as a root dependency. Use the FlexibleParent attribute if {this.Type.Name} is being used in a scene or only sometimes has a parent available, or if the type of the parent may change.");
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
                        this.ParentDependencies.Add(getPair(flexible: true));
                    }
                    else if (memberAccessor.Markers.Contains(nameof(SiblingAttribute))) {
                        var pair = getPair(flexible: false);
                        if (lineage == null) {
                            throw new ApplicationException($"{this.Type.Name} has a sibling attribute, but is being used as a root dependency. Use the FlexibleSibling attribute if {this.Type.Name} only sometimes has a parent available, or if the type of the parent may change.");
                        }
                        else {
                            StaticParentType = parentType;
                            var siblingDepPair = lineage.Last().InstanceDependencies.FirstOrDefault(x => x.Instantiator.Type == pair.Instantiator.Type);
                            if (siblingDepPair == null) {
                                throw new ApplicationException($"{this.Type.Name}.{pair.Accessor.Name} has a sibling dependency of type {pair.Accessor.Type.Name}. Its parent {lineage.Last().Type.Name} does not have an Instance dependency which matches this. Use the FlexibleSibling attribute if {this.Type.Name} only sometimes has a parent available, or if the type of the parent may change.");
                            }
                            else {
                                pair.SiblingDependencyPair = siblingDepPair;
                            }
                        }
                        this.SiblingDependencies.Add(pair);
                    }
                    else if (memberAccessor.Markers.Contains(nameof(FlexibleSiblingAttribute))) {
                        this.SiblingDependencies.Add(getPair(flexible: true));
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

            return true;
        }

        private object CreateNew() {
            return RicochetInstantiator.New();
        }

        internal void ResolveDependencies(object instance, DependencyType dependencyType, object parent, ActinInstantiator parentInstantiator, Director director) {
            if (director is null) {
                throw new NullReferenceException("Director may not be null.");
            }
            var asActor = instance as Actor_SansType;
            if (asActor != null) {
                asActor.Instantiator = this; //Used for automatically disposing child dependencies.
                asActor.Util = new ActorUtil(asActor, director.Clock);
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


            if (instance != null) { //It should never be null, but it doesn't hurt to be safe.
                //Note that it's important that we called this before we resolved child dependencies.
                //The director assumes that the order it receives dependencies in is the order in
                //which they should be initialized.
                director.RegisterInjectedDependency(instance);
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
                    else if(parentInstantiator != null) {
                        //Flexible Sibling:
                        foreach (var parentInstanceDep in parentInstantiator.InstanceDependencies) {
                            if (parentInstanceDep.Instantiator == this) {
                                continue;
                            }
                            if (dep.Accessor.Type.IsAssignableFrom(parentInstanceDep.Instantiator.Type)) {
                                var siblingInstance = parentInstanceDep.Accessor.GetVal(parent);
                                if (siblingInstance != null && siblingInstance != instance) {
                                    try {
                                        dep.Accessor.SetVal(instance, siblingInstance);
                                        break;
                                    }
                                    catch(Exception ex) {
                                        throw new ApplicationException($"Actin failed to set sibling dependency {this.Type.Name}.{dep.Accessor.Name} when using parent type {parentInstantiator?.Type.Name ?? "'Not Specified'"} and parent instance {parent ?? "null"}.", ex);
                                    }
                                }
                            }
                        }
                    }
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
                        try {
                            asDisposable?.Dispose();
                        }
                        finally {
                            if (childInstance is Actor_SansType) {
                                //Let it dispose its own child dependencies.
                            }
                            else {
                                //Dispose its child dependencies now:
                                dep.Instantiator?.DisposeChildren(childInstance);
                            }
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

        public bool HasSingletonInstance {
            get {
                lock(lockSingleton) return singleton != null;
            }
        }

        public bool WasBuilt => runBefore;

        internal object GetSingletonInstance(Director director) {
            lock (lockSingleton) {
                if (singleton == null) {
                    singleton = providedSingletonInstance ?? CreateNew();
                    ResolveDependencies(singleton, DependencyType.Singleton, null, null, director);
                }
                return singleton;
            }
        }

        internal object GetInstance(Director director, object parent, ActinInstantiator parentInstantiator) {
            var instance = CreateNew();
            ResolveDependencies(instance, DependencyType.Instance, parent, parentInstantiator, director);
            return instance;
        }

        class AccessorInstantiatorPair {
            //This is expected to be null when a parent or sibling is flexible:
            public ActinInstantiator Instantiator;
            public PropertyAndFieldAccessor Accessor;
            //Used for getting a sibling member from a parent:
            public AccessorInstantiatorPair SiblingDependencyPair;
        }

        class AccessorInstantiatorPairWithInstance {
            public AccessorInstantiatorPair Pair;
            public object Instance;
        }

        public override string ToString() {
            return $"{this.Type.Name} Instantiator";
        }
    }

    public enum DependencyType {
        Singleton,
        Instance,
    }
}
