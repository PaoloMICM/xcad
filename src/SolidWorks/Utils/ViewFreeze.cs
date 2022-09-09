﻿using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.SolidWorks.Documents;

namespace Xarial.XCad.SolidWorks.Utils
{
    internal class ViewFreeze : IDisposable
    {
        private readonly IModelDoc2 m_Model;
        private readonly IModelView m_View;

        internal ViewFreeze(ISwDocument doc)
        {
            m_Model = doc.Model;

            m_Model.FeatureManager.EnableFeatureTree = false;
            m_Model.FeatureManager.EnableFeatureTreeWindow = false;

            m_View = m_Model.IActiveView;

            if (m_View != null)
            {
                m_View.EnableGraphicsUpdate = false;
            }
        }

        public void Dispose()
        {
            m_Model.FeatureManager.EnableFeatureTree = true;
            m_Model.FeatureManager.EnableFeatureTreeWindow = true;

            if (m_View != null)
            {
                m_View.EnableGraphicsUpdate = true;
            }
        }
    }
}
