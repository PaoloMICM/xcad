﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Linq;
using Xarial.XCad.Base;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Delegates;
using Xarial.XCad.Geometry;
using Xarial.XCad.Geometry.Structures;
using Xarial.XCad.SolidWorks.Documents.EventHandlers;
using Xarial.XCad.SolidWorks.Geometry;
using Xarial.XCad.Utils.Diagnostics;

namespace Xarial.XCad.SolidWorks.Documents
{
    public interface ISwPart : ISwDocument3D, IXPart 
    {
        IPartDoc Part { get; }
    }

    internal class SwPart : SwDocument3D, ISwPart
    {
        public IPartDoc Part => Model as IPartDoc;

        public IXBodyRepository Bodies { get; }

        private readonly CutListRebuildEventsHandler m_CutListRebuild;

        public event CutListRebuildDelegate CutListRebuild 
        {
            add
            {
                m_CutListRebuild.Attach(value);
            }
            remove
            {
                m_CutListRebuild.Detach(value);
            }
        }

        internal SwPart(IPartDoc part, ISwApplication app, IXLogger logger, bool isCreated)
            : base((IModelDoc2)part, app, logger, isCreated)
        {
            m_CutListRebuild = new CutListRebuildEventsHandler(this, app);

            Bodies = new SwPartBodyCollection(this);
        }

        internal protected override swDocumentTypes_e? DocumentType => swDocumentTypes_e.swDocPART;

        protected override bool IsLightweightMode => false;
        protected override bool IsRapidMode => false;

        public override IXBoundingBox PreCreateBoundingBox()
            => new SwPartBoundingBox(this, OwnerApplication);
    }

    internal class SwPartBodyCollection : SwBodyCollection
    {
        private SwPart m_Part;

        public SwPartBodyCollection(SwPart rootDoc) : base(rootDoc)
        {
            m_Part = rootDoc;
        }

        protected override IEnumerable<IBody2> GetSwBodies()
            => (m_Part.Part.GetBodies2((int)swBodyType_e.swAllBodies, false) as object[])?.Cast<IBody2>();
    }
}