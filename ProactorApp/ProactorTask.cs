using System;
using System.Collections.Generic;
using System.Text;

namespace ProactorApp
{
    public class ProactorTask
    {
        public bool Finished { get; set; } = false;
        public object Result { get; set; } = null;
    }
}
