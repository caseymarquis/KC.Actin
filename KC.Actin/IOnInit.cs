using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin {
    /// <summary>
    /// If a director resolves a dependency which is not an actor, that dependency may implement
    /// this interface in order to initialize itself after it has been created.
    /// </summary>
    public interface IOnInit {
        /// <summary>
        /// This will be run after the director creates the object.
        /// </summary>
        Task OnInit(ActorUtil util);
    }
}
