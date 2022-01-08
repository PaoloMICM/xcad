﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xarial.XCad.Geometry.Wires;
using Xarial.XCad.Sketch;
using Xarial.XCad.SolidWorks.Documents;

namespace Xarial.XCad.SolidWorks.Sketch
{
    public interface ISwSketchSpline : ISwSketchSegment, IXSketchSpline
    {
        ISketchSpline Spline { get; }
    }

    internal class SwSketchSpline : SwSketchSegment, ISwSketchSpline
    {
        public ISketchSpline Spline => (ISketchSpline)Segment;

        public override IXSketchPoint StartPoint => OwnerDocument.CreateObjectFromDispatch<SwSketchPoint>((Spline.GetPoints2() as object[]).First());
        public override IXSketchPoint EndPoint => OwnerDocument.CreateObjectFromDispatch<SwSketchPoint>((Spline.GetPoints2() as object[]).Last());

        internal SwSketchSpline(ISketchSpline spline, ISwDocument doc, ISwApplication app, bool created)
            : base((ISketchSegment)spline, doc, app, created)
        {
        }

        protected override ISketchSegment CreateSketchEntity()
        {
            throw new NotImplementedException();
        }
    }
}
