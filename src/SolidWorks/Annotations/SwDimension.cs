﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Runtime.InteropServices;
using Xarial.XCad.Annotations;
using Xarial.XCad.Annotations.Delegates;
using Xarial.XCad.SolidWorks.Annotations.EventHandlers;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.Toolkit.Services;

namespace Xarial.XCad.SolidWorks.Annotations
{
    public interface ISwDimension : IXDimension, IDisposable, ISwSelObject
    {
        IDimension Dimension { get; }
        IDisplayDimension DisplayDimension { get; }
    }

    internal class SwDimension : SwSelObject, ISwDimension
    {
        private IDimension m_Dimension;

        private SwDimensionChangeEventsHandler m_ValueChangedHandler;

        public event DimensionValueChangedDelegate ValueChanged
        {
            add
            {
                m_ValueChangedHandler.Attach(value);
            }
            remove
            {
                m_ValueChangedHandler.Detach(value);
            }
        }

        public IDimension Dimension => m_Dimension ?? (m_Dimension = DisplayDimension.GetDimension2(0));
        public IDisplayDimension DisplayDimension { get; private set; }

        public string Name 
        {
            get 
            {
                var fullName = Dimension.FullName;
                var nameParts = fullName.Split('@');

                return $"{nameParts[0]}@{nameParts[1]}";
            }
        }

        internal SwDimension(IDisplayDimension dispDim, ISwDocument doc, ISwApplication app)
            : base(dispDim, doc, app)
        {
            if (doc == null) 
            {
                throw new ArgumentNullException(nameof(doc));
            }

            DisplayDimension = dispDim;

            m_ValueChangedHandler = new SwDimensionChangeEventsHandler(this, doc);
        }

        public virtual double GetValue(string confName = "")
        {
            var dim = DisplayDimension.GetDimension2(0);

            swInConfigurationOpts_e opts;
            string[] confs;

            if (!string.IsNullOrEmpty(confName))
            {
                confs = new string[] { confName };
                opts = swInConfigurationOpts_e.swSpecifyConfiguration;
            }
            else
            {
                opts = swInConfigurationOpts_e.swThisConfiguration;
                confs = null;
            }

            var val = (dim.GetSystemValue3((int)opts, confs) as double[])[0];

            return val;
        }

        public void SetValue(double val, string confName = "")
        {
            swInConfigurationOpts_e opts;
            string[] confs;

            if (!string.IsNullOrEmpty(confName))
            {
                confs = new string[] { confName };
                opts = swInConfigurationOpts_e.swSpecifyConfiguration;
            }
            else
            {
                opts = swInConfigurationOpts_e.swAllConfiguration;
                confs = null;
            }

            Dimension.SetSystemValue3(val, (int)opts, confs);
        }

        public void Dispose()
        {
            m_ValueChangedHandler.Dispose();

            Dispose(true);

            //NOTE: releasing the pointers as unreleased pointer might cause crash
            if (m_Dimension != null && Marshal.IsComObject(m_Dimension))
            {
                Marshal.ReleaseComObject(m_Dimension);
                m_Dimension = null;
            }

            if (DisplayDimension != null && Marshal.IsComObject(DisplayDimension))
            {
                Marshal.ReleaseComObject(DisplayDimension);
                DisplayDimension = null;
            }

            GC.Collect();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public override void Select(bool append)
        {
            if (!DisplayDimension.IGetAnnotation().Select3(append, null))
            {
                throw new Exception("Failed to select dimension");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}