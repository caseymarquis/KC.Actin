using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin.Interfaces {
    internal interface ICreateInstanceActorForScene {
        Actor_SansType CreateInstanceActorForScene(Type typeToCreate, Actor_SansType parent);
    }
}
