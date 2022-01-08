﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using System;
using Xarial.XCad.Geometry;
using Xarial.XCad.Geometry.Structures;
using Xarial.XCad.Geometry.Wires;
using Xarial.XCad.Sketch;
using Xarial.XCad.SolidWorks.Documents;

namespace Xarial.XCad.SolidWorks.Sketch
{
    public interface ISwSketchLine : IXSketchLine 
    {
        ISketchLine Line { get; }
    }

    internal class SwSketchLine : SwSketchSegment, ISwSketchLine
    {
        public ISketchLine Line => (ISketchLine)Segment;

        public override IXSketchPoint StartPoint => OwnerDocument.CreateObjectFromDispatch<SwSketchPoint>(Line.IGetStartPoint2());
        public override IXSketchPoint EndPoint => OwnerDocument.CreateObjectFromDispatch<SwSketchPoint>(Line.IGetEndPoint2());

        public Point StartCoordinate 
        {
            get 
            {
                if (IsCommitted)
                {
                    return StartPoint.Coordinate;
                }
                else 
                {
                    return m_Creator.CachedProperties.Get<Point>();
                }
            }
            set 
            {
                if (IsCommitted)
                {
                    StartPoint.Coordinate = value;
                }
                else 
                {
                    m_Creator.CachedProperties.Set(value);
                }
            }
        }
        
        public Point EndCoordinate 
        {
            get
            {
                if (IsCommitted)
                {
                    return EndPoint.Coordinate;
                }
                else
                {
                    return m_Creator.CachedProperties.Get<Point>();
                }
            }
            set
            {
                if (IsCommitted)
                {
                    EndPoint.Coordinate = value;
                }
                else
                {
                    m_Creator.CachedProperties.Set(value);
                }
            }
        }

        internal SwSketchLine(ISketchLine line, ISwDocument doc, ISwApplication app, bool created) 
            : base((ISketchSegment)line, doc, app, created)
        {
        }

        protected override ISketchSegment CreateSketchEntity()
        {
            var line = (ISketchLine)m_SketchMgr.CreateLine(
                StartCoordinate.X,
                StartCoordinate.Y,
                StartCoordinate.Z,
                EndCoordinate.X,
                EndCoordinate.Y,
                EndCoordinate.Z);
            
            return (ISketchSegment)line;
        }
    }
}