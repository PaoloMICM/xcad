﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Text;
using Xarial.XCad.Features;
using Xarial.XCad.Geometry.Structures;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks.Utils;

namespace Xarial.XCad.SolidWorks.Features
{
    public interface ISwCoordinateSystem : IXCoordinateSystem, ISwFeature
    {
    }

    internal class SwCoordinateSystem : SwFeature, ISwCoordinateSystem
    {
        private readonly ICoordinateSystemFeatureData m_CoordSys;

        internal SwCoordinateSystem(IFeature feat, ISwDocument doc, ISwApplication app, bool created) : base(feat, doc, app, created)
        {
            m_CoordSys = feat.GetDefinition() as ICoordinateSystemFeatureData;
        }

        public TransformMatrix Transform
            => m_CoordSys.Transform.ToTransformMatrix();
    }
}
