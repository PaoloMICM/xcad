﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using System;
using System.Collections.Generic;
using System.Text;
using Xarial.XCad.Base;
using Xarial.XCad.UI;

namespace Xarial.XCad.Documents
{
    /// <summary>
    /// Represents the drawing sheet
    /// </summary>
    public interface IXSheet : IXObject, IXTransaction
    {
        /// <summary>
        /// Name of the sheet
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Drawings views on this sheet
        /// </summary>
        IXDrawingViewRepository DrawingViews { get; }

        /// <summary>
        /// Preview of this drawing sheet
        /// </summary>
        IXImage Preview { get; }
    }
}
