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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Features;
using Xarial.XCad.Reflection;
using Xarial.XCad.Services;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents.Exceptions;
using Xarial.XCad.SolidWorks.Enums;
using Xarial.XCad.SolidWorks.Features;
using Xarial.XCad.SolidWorks.Utils;
using Xarial.XCad.UI;

namespace Xarial.XCad.SolidWorks.Documents
{
    public interface ISwConfiguration : ISwObject, IXConfiguration, IDisposable
    {
        IConfiguration Configuration { get; }
        new ISwCustomPropertiesCollection Properties { get; }
    }

    [DebuggerDisplay("{" + nameof(Name) + "}")]
    internal class SwConfiguration : SwObject, ISwConfiguration
    {
        internal const string QTY_PROPERTY = "UNIT_OF_MEASURE";

        public IConfiguration Configuration => m_Creator.Element;

        private readonly SwDocument3D m_Doc;

        public virtual string Name
        {
            get
            {
                if (m_Creator.IsCreated)
                {
                    return Configuration.Name;
                }
                else
                {
                    return m_Creator.CachedProperties.Get<string>();
                }
            }
            set
            {
                if (m_Creator.IsCreated)
                {
                    Configuration.Name = value;
                }
                else
                {
                    m_Creator.CachedProperties.Set(value);
                }
            }
        }

        IXPropertyRepository IPropertiesOwner.Properties => Properties;

        public virtual ISwCustomPropertiesCollection Properties => m_PropertiesLazy.Value;

        private readonly Lazy<ISwCustomPropertiesCollection> m_PropertiesLazy;

        public bool IsCommitted => m_Creator.IsCreated;

        public virtual IEnumerable<IXCutListItem> CutLists
        {
            get
            {
                var activeConf = m_Doc.Configurations.Active;

                var cutLists = ((SwFeatureManager)m_Doc.Features).EnumerateCutLists();

                if (cutLists.Any())
                {
                    if (activeConf.Configuration != this.Configuration)
                    {
                        throw new ConfigurationSpecificCutListNotSupportedException();
                    };
                }

                return cutLists;
            }
        }

        private readonly ElementCreator<IConfiguration> m_Creator;

        internal SwConfiguration(IConfiguration conf, SwDocument3D doc, ISwApplication app, bool created) : base(conf, doc, app)
        {
            m_Doc = doc;

            m_Creator = new ElementCreator<IConfiguration>(Create, conf, created);

            m_PropertiesLazy = new Lazy<ISwCustomPropertiesCollection>(
                () => new SwConfigurationCustomPropertiesCollection(Name, m_Doc, OwnerApplication));
        }

        public override object Dispatch => Configuration;

        public IXImage Preview
            => PictureDispUtils.PictureDispToXImage(OwnerApplication.Sw.GetPreviewBitmap(m_Doc.Path, Name));

        public string PartNumber => GetPartNumber(Configuration);

        public double Quantity 
        {
            get 
            {
                var qtyPrp = GetPropertyValue(Configuration.CustomPropertyManager, QTY_PROPERTY);

                if (string.IsNullOrEmpty(qtyPrp))
                {
                    qtyPrp = GetPropertyValue(m_Doc.Model.Extension.CustomPropertyManager[""], QTY_PROPERTY);
                }

                if (!string.IsNullOrEmpty(qtyPrp))
                {
                    var qtyStr = GetPropertyValue(Configuration.CustomPropertyManager, qtyPrp);

                    double qty;

                    if (!string.IsNullOrEmpty(qtyStr))
                    {
                        if (double.TryParse(qtyStr, out qty))
                        {
                            return qty;
                        }
                        else
                        {
                            return 1;
                        }
                    }
                    else
                    {
                        qtyStr = GetPropertyValue(m_Doc.Model.Extension.CustomPropertyManager[""], qtyPrp);

                        if (double.TryParse(qtyStr, out qty))
                        {
                            return qty;
                        }
                        else
                        {
                            return 1;
                        }
                    }
                }
                else
                {
                    return 1;
                }
            }
        }

        public BomChildrenSolving_e BomChildrenSolving 
        {
            get
            {
                if (m_Doc is ISwAssembly)
                {
                    var bomDispOpt = Configuration.ChildComponentDisplayInBOM;

                    switch ((swChildComponentInBOMOption_e)bomDispOpt)
                    {
                        case swChildComponentInBOMOption_e.swChildComponent_Show:
                            return BomChildrenSolving_e.Show;

                        case swChildComponentInBOMOption_e.swChildComponent_Hide:
                            return BomChildrenSolving_e.Hide;

                        case swChildComponentInBOMOption_e.swChildComponent_Promote:
                            return BomChildrenSolving_e.Promote;

                        default:
                            throw new NotSupportedException($"Not supported BOM display option: {bomDispOpt}");
                    }
                }
                else 
                {
                    return BomChildrenSolving_e.Show;
                }
            }
        }

        private string GetPropertyValue(ICustomPropertyManager prpMgr, string prpName) 
        {
            string resVal;

            if (OwnerApplication.IsVersionNewerOrEqual(SwVersion_e.Sw2018))
            {
                prpMgr.Get6(prpName, false, out _, out resVal, out _, out _);
            }
            else if (OwnerApplication.IsVersionNewerOrEqual(SwVersion_e.Sw2014))
            {
                prpMgr.Get5(prpName, false, out _, out resVal, out _);
            }
            else
            {
                prpMgr.Get4(prpName, false, out _, out resVal);
            }

            return resVal;
        }

        private string GetPartNumber(IConfiguration conf) 
        {
            switch ((swBOMPartNumberSource_e)conf.BOMPartNoSource)
            {
                case swBOMPartNumberSource_e.swBOMPartNumber_ConfigurationName:
                    return conf.Name;
                case swBOMPartNumberSource_e.swBOMPartNumber_DocumentName:
                    return Path.GetFileNameWithoutExtension(m_Doc.Title);
                case swBOMPartNumberSource_e.swBOMPartNumber_ParentName:
                    return GetPartNumber(conf.GetParent());
                case swBOMPartNumberSource_e.swBOMPartNumber_UserSpecified:
                    return conf.AlternateName;
                default:
                    throw new NotSupportedException();
            }
        }

        public virtual void Commit(CancellationToken cancellationToken) => m_Creator.Create(cancellationToken);

        private IConfiguration Create(CancellationToken cancellationToken) 
        {
            IConfiguration conf;

            if (OwnerApplication.IsVersionNewerOrEqual(SwVersion_e.Sw2018))
            {
                conf = m_Doc.Model.ConfigurationManager.AddConfiguration2(Name, "", "", (int)swConfigurationOptions2_e.swConfigOption_DontActivate, "", "", false);
            }
            else 
            {
                conf = m_Doc.Model.ConfigurationManager.AddConfiguration(Name, "", "", (int)swConfigurationOptions2_e.swConfigOption_DontActivate, "", "");
            }

            if (conf == null) 
            {
                throw new Exception("Failed to create configuration");
            }

            return conf;
        }

        public void Dispose()
        {
            if (m_PropertiesLazy.IsValueCreated) 
            {
                m_PropertiesLazy.Value.Dispose();
            }
        }
    }

    internal class SwComponentConfiguration : SwConfiguration
    {
        private static IConfiguration GetConfiguration(SwComponent comp) 
        {
            var doc = comp.ReferencedDocument;

            if (doc.IsCommitted)
            {
                return (IConfiguration)doc.Model.GetConfigurationByName(comp.Component.ReferencedConfiguration);
            }
            else
            {
                return null;
            }
        }

        private readonly SwComponent m_Comp;

        internal SwComponentConfiguration(SwComponent comp, ISwApplication app) 
            : this(GetConfiguration(comp), (SwDocument3D)comp.ReferencedDocument, app, comp.Component.ReferencedConfiguration)
        {
            m_Comp = comp;
        }

        private SwComponentConfiguration(IConfiguration conf, SwDocument3D doc, ISwApplication app, string name) 
            : base(conf, doc, app, conf != null)
        {
            if (conf == null) 
            {
                Name = name;
            }
        }

        public override IEnumerable<IXCutListItem> CutLists => ((SwComponentFeatureManager)m_Comp.Features).EnumerateCutLists();
    }

    internal class SwViewOnlyUnloadedConfiguration : SwConfiguration
    {
        public override string Name
        {
            get => m_ViewOnlyConfName;
            set => throw new NotSupportedException("Name of view-only configuration cannot be changed");
        }

        private string m_ViewOnlyConfName;

        internal SwViewOnlyUnloadedConfiguration(string confName, SwDocument3D doc, ISwApplication app)
            : base(null, doc, app, false)
        {
            m_ViewOnlyConfName = confName;
        }

        public override void Commit(CancellationToken cancellationToken) => throw new InactiveLdrConfgurationNotSupportedException();
        public override IEnumerable<IXCutListItem> CutLists => throw new InactiveLdrConfgurationNotSupportedException();
        public override object Dispatch => throw new InactiveLdrConfgurationNotSupportedException();
        public override ISwCustomPropertiesCollection Properties => throw new InactiveLdrConfgurationNotSupportedException();
    }

    internal class SwLdrUnloadedConfiguration : SwAssemblyConfiguration
    {
        public override string Name 
        {
            get => m_LdrConfName;
            set => throw new NotSupportedException("Name of inactive LDR configuration cannot be changed");
        }

        private string m_LdrConfName;

        internal SwLdrUnloadedConfiguration(SwAssembly assm, ISwApplication app, string confName) 
            : base(null, assm, app, false)
        {
            m_LdrConfName = confName;
        }

        public override void Commit(CancellationToken cancellationToken) => throw new InactiveLdrConfgurationNotSupportedException();
        public override IEnumerable<IXCutListItem> CutLists => throw new InactiveLdrConfgurationNotSupportedException();
        public override object Dispatch => throw new InactiveLdrConfgurationNotSupportedException();
        public override ISwCustomPropertiesCollection Properties => throw new InactiveLdrConfgurationNotSupportedException();
    }
}