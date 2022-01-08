﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using Xarial.XCad.Base;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Delegates;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Features;
using Xarial.XCad.SolidWorks.Documents.EventHandlers;
using Xarial.XCad.UI;

namespace Xarial.XCad.SolidWorks.Documents
{
    public interface ISwConfigurationCollection : IXConfigurationRepository, IDisposable
    {
        new ISwConfiguration PreCreate();
        new ISwConfiguration Active { get; set; }
    }

    internal class SwConfigurationCollection : ISwConfigurationCollection
    {
        public event ConfigurationActivatedDelegate ConfigurationActivated 
        {
            add 
            {
                m_ConfigurationActivatedEventsHandler.Attach(value);
            }
            remove 
            {
                m_ConfigurationActivatedEventsHandler.Detach(value);
            }
        }

        IXConfiguration IXConfigurationRepository.Active 
        {
            get => Active;
            set => Active = (ISwConfiguration)value;
        }

        IXConfiguration IXConfigurationRepository.PreCreate() => PreCreate();

        protected readonly ISwApplication m_App;
        private readonly SwDocument3D m_Doc;

        private readonly ConfigurationActivatedEventsHandler m_ConfigurationActivatedEventsHandler;

        internal SwConfigurationCollection(SwDocument3D doc, ISwApplication app) 
        {
            m_App = app;
            m_Doc = doc;
            m_ConfigurationActivatedEventsHandler = new ConfigurationActivatedEventsHandler(doc, app);
        }

        public IXConfiguration this[string name] => this.Get(name);

        public bool TryGet(string name, out IXConfiguration ent)
        {
            var conf = m_Doc.Model.GetConfigurationByName(name);

            if (conf != null)
            {
                ent = m_Doc.CreateObjectFromDispatch<SwConfiguration>((IConfiguration)conf);
                return true;
            }
            else 
            {
                if (m_Doc.Model.IsOpenedViewOnly())
                {
                    ent = CreateViewOnlyConfiguration(name);
                    return true;
                }
                else
                {
                    ent = null;
                    return false;
                }
            }
        }

        protected virtual ISwConfiguration CreateViewOnlyConfiguration(string name)
            => new SwViewOnlyUnloadedConfiguration(name, m_Doc, m_App);

        public int Count => m_Doc.Model.GetConfigurationCount();

        public ISwConfiguration Active
        {
            get 
            {
                if (m_Doc.IsCommitted)
                {
                    return m_Doc.CreateObjectFromDispatch<SwConfiguration>(m_Doc.Model.ConfigurationManager.ActiveConfiguration);
                }
                else 
                {
                    var activeConfName = m_App.Sw.GetActiveConfigurationName(m_Doc.Path);
                    return new SwConfiguration(null, m_Doc, m_App, false)
                    {
                        Name = activeConfName
                    };
                }
            } 
            set 
            {
                if (m_Doc.Model.ConfigurationManager.ActiveConfiguration != value.Configuration)
                {
                    if (!m_Doc.Model.ShowConfiguration2(value.Name))
                    {
                        throw new Exception($"Failed to activate configuration '{value.Name}'");
                    }
                }
            }
        }

        public ISwConfiguration PreCreate() => new SwConfiguration(null, m_Doc, m_App, false);

        public void AddRange(IEnumerable<IXConfiguration> ents)
        {
            foreach (var conf in ents) 
            {
                conf.Commit();
            }
        }
        
        public void Dispose()
        {
        }

        public IEnumerator<IXConfiguration> GetEnumerator() => new SwConfigurationEnumerator(m_App, m_Doc);

        public void RemoveRange(IEnumerable<IXConfiguration> ents)
        {
            foreach (var conf in ents) 
            {
                if (conf.IsCommitted)
                {
                    if (Count == 1) 
                    {
                        throw new Exception("Cannot delete the last configuration");
                    }

                    if (string.Equals(Active.Name, conf.Name)) 
                    {
                        Active = (ISwConfiguration)this.First(c => !string.Equals(c.Name, conf.Name, StringComparison.CurrentCultureIgnoreCase));
                    }

                    if (!m_Doc.Model.DeleteConfiguration2(conf.Name)) 
                    {
                        throw new Exception($"Failed to delete configuration '{conf.Name}'");
                    }
                }
                else 
                {
                    throw new Exception($"Cannot delete uncommited configuration '{conf.Name}'");
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal class SwConfigurationEnumerator : SwConfigurationEnumeratorBase<SwConfiguration>
    {
        public SwConfigurationEnumerator(ISwApplication app, SwDocument3D doc) : base(app, doc)
        {
        }

        protected override SwConfiguration CreateViewOnlyConfiguration(string confName)
            => new SwViewOnlyUnloadedConfiguration(confName, m_Doc, m_App);

        protected override SwConfiguration PreCreateNewConfiguration()
            => new SwConfiguration(null, m_Doc, m_App, false);
    }

    internal abstract class SwConfigurationEnumeratorBase<TConf> : IEnumerator<TConf>
        where TConf: ISwConfiguration
    {
        public TConf Current
        {
            get 
            {
                var confName = m_ConfNames[m_CurConfIndex];

                if (m_Doc.IsCommitted)
                {
                    var conf = (IConfiguration)m_Doc.Model.GetConfigurationByName(confName);
                    
                    if (conf == null)
                    {
                        if (m_Doc.Model.IsOpenedViewOnly())
                        {
                            return CreateViewOnlyConfiguration(confName);
                        }
                        else
                        {
                            throw new Exception("Failed to get the configuration by name");

                        }
                    }
                    else
                    {
                        return m_Doc.CreateObjectFromDispatch<TConf>(conf);
                    }
                }
                else
                {
                    var conf = PreCreateNewConfiguration();
                    conf.Name = confName;
                    return conf;
                }
            }
        }

        object IEnumerator.Current => Current;

        private int m_CurConfIndex;

        protected readonly ISwApplication m_App;
        protected readonly SwDocument3D m_Doc;

        private string[] m_ConfNames;

        internal SwConfigurationEnumeratorBase(ISwApplication app, SwDocument3D doc)
        {
            m_App = app;
            m_Doc = doc;

            m_CurConfIndex = -1;

            if (doc.IsCommitted)
            {
                m_ConfNames = (string[])m_Doc.Model.GetConfigurationNames();
            }
            else 
            {
                m_ConfNames = (string[])m_App.Sw.GetConfigurationNames(m_Doc.Path);
            }
        }

        public bool MoveNext()
        {
            m_CurConfIndex++;
            return m_CurConfIndex < m_ConfNames.Length;
        }

        public void Reset()
        {
            m_CurConfIndex = -1;
        }

        protected abstract TConf PreCreateNewConfiguration();
        protected abstract TConf CreateViewOnlyConfiguration(string confName);

        public void Dispose()
        {
        }
    }

    public interface ISwAssemblyConfigurationCollection : ISwConfigurationCollection, IXAssemblyConfigurationRepository 
    {
        new ISwAssemblyConfiguration this[string name] { get; }
        new ISwAssemblyConfiguration PreCreate();
        new ISwAssemblyConfiguration Active { get; set; }
    }

    internal class SwAssemblyConfigurationCollection : SwConfigurationCollection, ISwAssemblyConfigurationCollection
    {
        private readonly SwAssembly m_Assm;

        internal SwAssemblyConfigurationCollection(SwAssembly assm, ISwApplication app) : base(assm, app)
        {
            m_Assm = assm;
        }

        IXAssemblyConfiguration IXRepository<IXAssemblyConfiguration>.this[string name] => (this as ISwAssemblyConfigurationCollection)[name];

        ISwAssemblyConfiguration ISwAssemblyConfigurationCollection.this[string name] => (ISwAssemblyConfiguration)base[name];

        ISwAssemblyConfiguration ISwAssemblyConfigurationCollection.Active 
        {
            get => (ISwAssemblyConfiguration)base.Active;
            set => base.Active = value;
        }

        IXAssemblyConfiguration IXAssemblyConfigurationRepository.Active 
        {
            get => (this as ISwAssemblyConfigurationCollection).Active;
            set => this.Active = (ISwAssemblyConfiguration)value;
        }

        public void AddRange(IEnumerable<IXAssemblyConfiguration> ents)
            => base.AddRange(ents);

        public void RemoveRange(IEnumerable<IXAssemblyConfiguration> ents)
            => base.RemoveRange(ents);

        public bool TryGet(string name, out IXAssemblyConfiguration ent)
        {
            var res = base.TryGet(name, out IXConfiguration conf);
            ent = (IXAssemblyConfiguration)conf;
            return res;
        }

        protected override ISwConfiguration CreateViewOnlyConfiguration(string name)
            => new SwLdrUnloadedConfiguration(m_Assm, m_App, name);

        IEnumerator<IXAssemblyConfiguration> IEnumerable<IXAssemblyConfiguration>.GetEnumerator()
            => new SwAssemblyConfigurationEnumerator(m_App, m_Assm);

        ISwAssemblyConfiguration ISwAssemblyConfigurationCollection.PreCreate()
            => new SwAssemblyConfiguration(null, m_Assm, m_App, false);

        IXAssemblyConfiguration IXAssemblyConfigurationRepository.PreCreate()
            => (this as ISwAssemblyConfigurationCollection).PreCreate();
    }

    internal class SwAssemblyConfigurationEnumerator : SwConfigurationEnumeratorBase<SwAssemblyConfiguration>
    {
        private readonly SwAssembly m_Assm;

        public SwAssemblyConfigurationEnumerator(ISwApplication app, SwAssembly assm) : base(app, assm)
        {
            m_Assm = assm;
        }

        protected override SwAssemblyConfiguration CreateViewOnlyConfiguration(string confName)
            => new SwLdrUnloadedConfiguration(m_Assm, m_App, confName);

        protected override SwAssemblyConfiguration PreCreateNewConfiguration()
            => new SwAssemblyConfiguration(null, m_Assm, m_App, false);
    }
}
