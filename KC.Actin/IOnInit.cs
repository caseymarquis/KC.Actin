using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin {
    interface IOnInit {
        Task OnInit(ActorUtil util);
    }
}
