using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin.Interfaces {
    public interface ICreateInstanceActorForScene {
        Actor_SansType _CreateInstanceActorForScene_(Type typeToCreate, Actor_SansType parent);
    }
}
