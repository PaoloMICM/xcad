﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using Xarial.XCad.Geometry;

namespace Xarial.XCad.SwDocumentManager.Geometry
{
    public interface ISwDmSolidBody : ISwDmBody, IXSolidBody 
    {
    }

    internal class SwDmSolidBody : SwDmBody, ISwDmSolidBody
    {
    }
}
