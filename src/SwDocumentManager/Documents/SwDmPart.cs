﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.swdocumentmgr;
using System;
using System.Collections.Generic;
using System.Text;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Delegates;
using Xarial.XCad.Geometry;

namespace Xarial.XCad.SwDocumentManager.Documents
{
    public interface ISwDmPart : ISwDmDocument3D, IXPart
    {
    }

    internal class SwDmPart : SwDmDocument3D, ISwDmPart
    {
        #region Not Supported
        
        public event CutListRebuildDelegate CutListRebuild;

        #endregion

        public SwDmPart(ISwDmApplication dmApp, ISwDMDocument doc, bool isCreated,
            Action<ISwDmDocument> createHandler, Action<ISwDmDocument> closeHandler, bool? isReadOnly)
            : base(dmApp, doc, isCreated, createHandler, closeHandler, isReadOnly)
        {
        }

        public IXBodyRepository Bodies => throw new NotImplementedException();
    }

    internal class SwDmVirtualPart : SwDmPart
    {
        private readonly SwDmDocument m_Owner;

        public SwDmVirtualPart(ISwDmApplication dmApp, ISwDMDocument doc, SwDmDocument owner, bool isCreated,
            Action<ISwDmDocument> createHandler, Action<ISwDmDocument> closeHandler, bool? isReadOnly) 
            : base(dmApp, doc, isCreated, createHandler, closeHandler, isReadOnly)
        {
            m_Owner = owner;
            m_Owner.Disposed += OnOwnerDisposed;
        }

        private void OnOwnerDisposed(SwDmDocument owner)
        {
            this.Close();
        }

        public override string Title
        {
            get => SwDmVirtualDocumentHelper.GetTitle(base.Title);
            set => base.Title = value;
        }

        public override bool IsDirty 
        {
            get => base.IsDirty;
            set
            {
                base.IsDirty = value;

                if (value) 
                {
                    m_Owner.IsDirty = true;
                }
            }
        }
    }
}
