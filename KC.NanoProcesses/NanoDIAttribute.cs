using System;
using System.Collections.Generic;
using System.Text;

namespace KC.NanoProcesses
{
    /// <summary>
    /// If this attribute is used on a class, then a single instance of the class will be created and stored
    /// for creating other NanoDI marked classes which contain the class in their constructor.
    /// 
    /// If the class in question extends NanoProcess, then it will also be added to the process pool.
    ///
    /// In summary, this attribute automates adding a NanoProcess and its dependencies to the main process pool.
    /// </summary>
    public class NanoDIAttribute : Attribute
    {
    }
}
