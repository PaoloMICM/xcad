﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.swdocumentmgr;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xarial.XCad.Data;
using Xarial.XCad.SwDocumentManager.Documents;
using Xarial.XCad.SwDocumentManager.Features;
using Xarial.XCad.Toolkit.Data;

namespace Xarial.XCad.SwDocumentManager
{
    public interface ISwDmObject : IXObject
    {
        object Dispatch { get; }
    }

    internal class SwDmObject : ISwDmObject
    {
        #region NotSuppoted

        public virtual bool IsAlive => throw new NotSupportedException();

        public virtual void Serialize(Stream stream)
            => throw new NotSupportedException();

        #endregion

        public ITagsManager Tags => m_TagsLazy.Value;

        private readonly Lazy<ITagsManager> m_TagsLazy;

        public SwDmObject(object disp)
        {
            Dispatch = disp;
            m_TagsLazy = new Lazy<ITagsManager>(() => new TagsManager());
        }

        public virtual object Dispatch { get; }

        public virtual bool Equals(IXObject other)
        {
            if (other is ISwDmObject)
            {
                return (other as ISwDmObject).Dispatch == Dispatch;
            }
            else 
            {
                return false;
            }
        }
    }

    public static class SwDmObjectFactory 
    {
        internal static TObj FromDispatch<TObj>(object disp, ISwDmDocument doc)
            where TObj : ISwDmObject
        {
            return (TObj)FromDispatch(disp, doc);
        }

        private static ISwDmObject FromDispatch(object disp, ISwDmDocument doc)
        {
            switch (disp) 
            {
                case ISwDMConfiguration conf:
                    switch (doc) 
                    {
                        case SwDmAssembly assm:
                            return new SwDmAssemblyConfiguration(conf, assm);

                        case SwDmDocument3D doc3D:
                            return new SwDmConfiguration(conf, doc3D);

                        default:
                            throw new NotSupportedException("This document type is not supported for configuration");
                    }

                case ISwDMCutListItem cutList:
                    return new SwDmCutListItem((ISwDMCutListItem2)cutList, (SwDmDocument3D)doc);

                case ISwDMComponent comp:
                    return new SwDmComponent((SwDmAssembly)doc, comp);

                case ISwDMSheet sheet:
                    return new SwDmSheet(sheet, (SwDmDrawing)doc);

                case ISwDMView view:
                    return new SwDmDrawingView(view, (SwDmDrawing)doc);

                default:
                    return new SwDmObject(disp);
            }
        }
    }
}
