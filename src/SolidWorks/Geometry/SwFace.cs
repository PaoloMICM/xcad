﻿//*********************************************************************
//xCAD
//Copyright(C) 2022 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xarial.XCad.Features;
using Xarial.XCad.Geometry;
using Xarial.XCad.Geometry.Structures;
using Xarial.XCad.Geometry.Surfaces;
using Xarial.XCad.Geometry.Wires;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks.Features;
using Xarial.XCad.SolidWorks.Geometry.Curves;
using Xarial.XCad.SolidWorks.Geometry.Surfaces;
using Xarial.XCad.SolidWorks.Utils;
using Xarial.XCad.Utils.Reflection;

namespace Xarial.XCad.SolidWorks.Geometry
{
    public interface ISwFace : ISwEntity, IXFace 
    {
        IFace2 Face { get; }
        new ISwSurface Definition { get; }
        new IEnumerable<ISwEdge> Edges { get; }
    }

    internal abstract class SwFace : SwEntity, ISwFace
    {
        IXSurface IXFace.Definition => Definition;
        IEnumerable<IXEdge> IXFace.Edges => Edges;

        public IFace2 Face { get; }
        private readonly IMathUtility m_MathUtils;

        protected SwFace(IFace2 face, ISwDocument doc, ISwApplication app) : base((IEntity)face, doc, app)
        {
            Face = face;
            m_MathUtils = app.Sw.IGetMathUtility();
        }

        public override ISwBody Body => OwnerApplication.CreateObjectFromDispatch<ISwBody>(Face.GetBody(), OwnerDocument);

        public override IEnumerable<ISwEntity> AdjacentEntities 
        {
            get 
            {
                foreach (IEdge edge in (Face.GetEdges() as object[]).ValueOrEmpty())
                {
                    yield return OwnerApplication.CreateObjectFromDispatch<ISwEdge>(edge, OwnerDocument);
                }
            }
        }

        public double Area => Face.GetArea();

        private IComponent2 GetSwComponent() => (Face as IEntity).GetComponent() as IComponent2;

        public System.Drawing.Color? Color 
        {
            get => SwColorHelper.GetColor(GetSwComponent(), 
                (o, c) => Face.GetMaterialPropertyValues2((int)o, c) as double[]);
            set => SwColorHelper.SetColor(value, GetSwComponent(),
                (m, o, c) => Face.SetMaterialPropertyValues2(m, (int)o, c),
                (o, c) => Face.RemoveMaterialProperty2((int)o, c));
        }

        public ISwSurface Definition => OwnerApplication.CreateObjectFromDispatch<SwSurface>(Face.IGetSurface(), OwnerDocument);

        public IXFeature Feature 
        {
            get 
            {
                var feat = Face.IGetFeature();

                if (feat != null)
                {
                    return OwnerDocument.CreateObjectFromDispatch<ISwFeature>(feat);
                }
                else 
                {
                    return null;
                }
            }
        }

        public IEnumerable<ISwEdge> Edges => (Face.GetEdges() as object[])
            .Select(f => OwnerApplication.CreateObjectFromDispatch<ISwEdge>(f, OwnerDocument));

        public override Point FindClosestPoint(Point point)
            => new Point(((double[])Face.GetClosestPointOn(point.X, point.Y, point.Z)).Take(3).ToArray());

        public bool TryProjectPoint(Point point, Vector direction, out Point projectedPoint)
        {
            var dirVec = (MathVector)m_MathUtils.CreateVector(direction.ToArray());
            var startPt = (MathPoint)m_MathUtils.CreatePoint(point.ToArray());

            var resPt = Face.GetProjectedPointOn(startPt, dirVec);

            if (resPt != null)
            {
                projectedPoint = new Point((double[])resPt.ArrayData);
                return true;
            }
            else
            {
                projectedPoint = null;
                return false;
            }
        }

        public void GetUVBoundary(out double uMin, out double uMax, out double vMin, out double vMax)
        {
            var uvBounds = (double[])Face.GetUVBounds();

            uMin = uvBounds[0];
            uMax = uvBounds[1];
            vMin = uvBounds[2];
            vMax = uvBounds[3];
        }

        public void CalculateUVParameter(Point point, out double uParam, out double vParam)
        {
            var uvParam = (double[])Face.ReverseEvaluate(point.X, point.Y, point.Z);

            uParam = uvParam[0];
            vParam = uvParam[1];
        }
    }

    public interface ISwPlanarFace : ISwFace, IXPlanarFace, ISwRegion
    {
        new ISwPlanarSurface Definition { get; }
    }

    internal class SwPlanarFace : SwFace, ISwPlanarFace
    {
        IXSegment[] IXRegion.Boundary => Boundary;
        IXPlanarSurface IXPlanarFace.Definition => Definition;

        public SwPlanarFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }

        public new ISwPlanarSurface Definition => OwnerApplication.CreateObjectFromDispatch<SwPlanarSurface>(Face.IGetSurface(), OwnerDocument);

        public Plane Plane => Definition.Plane;

        public ISwCurve[] Boundary => Edges.Select(e => e.Definition).ToArray();
    }

    public interface ISwCylindricalFace : ISwFace, IXCylindricalFace
    {
        new ISwCylindricalSurface Definition { get; }
    }

    internal class SwCylindricalFace : SwFace, ISwCylindricalFace
    {
        IXCylindricalSurface IXCylindricalFace.Definition => Definition;

        public SwCylindricalFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }

        public new ISwCylindricalSurface Definition => OwnerApplication.CreateObjectFromDispatch<SwCylindricalSurface>(
            Face.IGetSurface(), OwnerDocument);
    }

    public interface ISwBlendXFace : ISwFace, IXBlendXFace
    {
    }

    internal class SwBlendFace : SwFace, ISwBlendXFace
    {
        public SwBlendFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }
    }

    public interface ISwBFace : ISwFace, IXBFace
    {
    }

    internal class SwBFace : SwFace, ISwBFace
    {
        public SwBFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }
    }

    public interface ISwConicalFace : ISwFace, IXConicalFace
    {
    }

    internal class SwConicalFace : SwFace, ISwConicalFace
    {
        public SwConicalFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }
    }

    public interface ISwExtrudedFace : ISwFace, IXExtrudedFace
    {
    }

    internal class SwExtrudedFace : SwFace, ISwExtrudedFace
    {
        public SwExtrudedFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }
    }

    public interface ISwOffsetFace : ISwFace, IXOffsetFace
    {
    }

    internal class SwOffsetFace : SwFace, ISwOffsetFace
    {
        public SwOffsetFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }
    }

    public interface ISwRevolvedFace : ISwFace, IXRevolvedFace
    {
    }

    internal class SwRevolvedFace : SwFace, ISwRevolvedFace
    {
        public SwRevolvedFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }
    }

    public interface ISwSphericalFace : ISwFace, IXSphericalFace
    {
    }

    internal class SwSphericalFace : SwFace, ISwSphericalFace
    {
        public SwSphericalFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }
    }

    public interface ISwToroidalFace : ISwFace, IXToroidalFace
    {
    }

    internal class SwToroidalFace : SwFace, ISwToroidalFace
    {
        public SwToroidalFace(IFace2 face, ISwDocument doc, ISwApplication app) : base(face, doc, app)
        {
        }
    }
}
