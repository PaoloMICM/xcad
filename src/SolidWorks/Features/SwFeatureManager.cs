﻿//*********************************************************************
//xCAD
//Copyright(C) 2022 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Xarial.XCad.Base;
using Xarial.XCad.Documents;
using Xarial.XCad.Features;
using Xarial.XCad.Features.CustomFeature;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks.Features.CustomFeature;
using Xarial.XCad.SolidWorks.Features.CustomFeature.Toolkit;
using Xarial.XCad.SolidWorks.Sketch;
using Xarial.XCad.SolidWorks.Utils;
using Xarial.XCad.Toolkit.CustomFeature;
using Xarial.XCad.Toolkit.Services;
using Xarial.XCad.Toolkit.Utils;
using Xarial.XCad.Utils.Reflection;

namespace Xarial.XCad.SolidWorks.Features
{
    public interface ISwFeatureManager : IXFeatureRepository
    {
        new ISwFeature this[string name] { get; }
    }

    /// <inheritdoc/>
    internal class SwFeatureManager : ISwFeatureManager
    {
        private IFeatureManager FeatMgr => Document.Model.FeatureManager;

        private readonly Lazy<MacroFeatureParametersParser> m_ParamsParserLazy;

        private readonly SwApplication m_App;
        internal SwDocument Document { get; }

        public int Count
        {
            get
            {
                if (Document.IsCommitted)
                {
                    return FeatMgr.GetFeatureCount(false);
                }
                else 
                {
                    return m_Cache.Count;
                }
            }
        }

        IXFeature IXRepository<IXFeature>.this[string name] => this[name];

        public ISwFeature this[string name] => (ISwFeature)RepositoryHelper.Get(this, name);

        public virtual bool TryGet(string name, out IXFeature ent)
        {
            if (Document.IsCommitted)
            {
                IFeature swFeat;

                switch (Document.Model)
                {
                    case IPartDoc part:
                        swFeat = part.FeatureByName(name) as IFeature;
                        break;

                    case IAssemblyDoc assm:
                        swFeat = assm.FeatureByName(name) as IFeature;
                        break;

                    case IDrawingDoc drw:
                        swFeat = drw.FeatureByName(name) as IFeature;
                        break;

                    default:
                        throw new NotSupportedException();
                }

                if (swFeat != null)
                {
                    var feat = Document.CreateObjectFromDispatch<SwFeature>(swFeat);
                    feat.SetContext(m_Context);
                    ent = feat;
                    return true;
                }
                else
                {
                    ent = null;
                    return false;
                }
            }
            else 
            {
                return m_Cache.TryGet(name, out ent);
            }
        }

        protected readonly Context m_Context;

        private readonly EntityCache<IXFeature> m_Cache;

        internal SwFeatureManager(SwDocument doc, SwApplication app, Context context)
        {
            m_App = app;
            Document = doc;
            m_Context = context;
            m_ParamsParserLazy = new Lazy<MacroFeatureParametersParser>(() => new MacroFeatureParametersParser(app));
            m_Cache = new EntityCache<IXFeature>(doc, this, f => f.Name);
        }

        public virtual void AddRange(IEnumerable<IXFeature> feats, CancellationToken cancellationToken)
        {
            if (Document.IsCommitted)
            {
                using (var viewFreeze = new ViewFreeze(Document))
                {
                    RepositoryHelper.AddRange(feats, cancellationToken);
                }
            }
            else 
            {
                m_Cache.AddRange(feats, cancellationToken);
            }
        }

        internal void CommitCache(CancellationToken cancellationToken) => m_Cache.Commit(cancellationToken);

        public virtual IEnumerator<IXFeature> GetEnumerator()
        {
            if (Document.IsCommitted)
            {
                return new DocumentFeatureEnumerator(Document, GetFirstFeature(), new Context(Document));
            }
            else 
            {
                return m_Cache.GetEnumerator();
            }
        }

        internal protected virtual IFeature GetFirstFeature() => Document.Model.IFirstFeature();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void RemoveRange(IEnumerable<IXFeature> ents, CancellationToken cancellationToken)
        {
            if (Document.IsCommitted)
            {
                using (var viewFreeze = new ViewFreeze(Document))
                {
                    var disps = ents.Cast<SwFeature>().Select(e => new DispatchWrapper(e.Feature)).ToArray();

                    if (Document.Model.Extension.MultiSelect2(disps, false, null) == disps.Length)
                    {
                        if (!Document.Model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed))
                        {
                            throw new Exception("Failed to delete features");
                        }
                    }
                    else
                    {
                        throw new Exception("Failed to select features for deletion");
                    }
                }
            }
            else 
            {
                m_Cache.RemoveRange(ents, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public void CreateCustomFeature<TDef, TParams, TPage>(TParams data)
            where TParams : class
            where TPage : class
            where TDef : class, IXCustomFeatureDefinition<TParams, TPage>, new()
        {
            var inst = (TDef)CustomFeatureDefinitionInstanceCache.GetInstance(typeof(TDef));
            inst.Insert(Document, data);
        }

        public void Enable(bool enable)
        {
            FeatMgr.EnableFeatureTree = enable;
            FeatMgr.EnableFeatureTreeWindow = enable;
        }

        public T PreCreate<T>() where T : IXFeature
        {
            if (typeof(T).IsAssignableToGenericType(typeof(IXCustomFeature<>)))
            {
                var macroFeatureParamsType = typeof(T).GetArgumentsOfGenericType(typeof(IXCustomFeature<>)).First();
                var feat = SwMacroFeature<object>.CreateSpecificInstance(null, Document, m_App, macroFeatureParamsType, m_ParamsParserLazy.Value);
                return (T)(object)feat;
            }
            else 
            {
                return RepositoryHelper.PreCreate<IXFeature, T>(this,
                    () => new SwSketch2D(default(ISketch), Document, m_App, false),
                    () => new SwSketch3D(default(ISketch), Document, m_App, false),
                    () => new SwMacroFeature(null, Document, m_App, false),
                    () => new SwDumbBody(null, Document, m_App, false),
                    () => new SwPlane(null, Document, m_App, false),
                    () => new SwSketchPicture(default(IFeature), Document, m_App, false));
            }
        }
    }

    internal class DocumentFeatureEnumerator : FeatureEnumerator
    {
        public DocumentFeatureEnumerator(ISwDocument rootDoc, IFeature firstFeat, Context context) : base(rootDoc, firstFeat, context)
        {
            Reset();
        }
    }

    internal static class SwFeatureManagerExtension 
    {
        internal static IEnumerable<SwCutListItem> IterateCutLists(this SwFeatureManager featMgr, ISwDocument3D parent, ISwConfiguration refConf)
        {
            foreach (var feat in FeatureEnumerator.IterateFeatures(featMgr.GetFirstFeature(), false)) 
            {
                if (feat.GetTypeName2() == "SolidBodyFolder") 
                {
                    foreach (var subFeat in FeatureEnumerator.IterateSubFeatures(feat, true)) 
                    {
                        if (subFeat.GetTypeName2() == "CutListFolder") 
                        {
                            var cutListFolder = (IBodyFolder)subFeat.GetSpecificFeature2();

                            if (cutListFolder.GetBodyCount() > 0)//no bodies for hidden cut-lists (not available in the specific configuration)
                            {
                                var cutList = featMgr.Document.CreateObjectFromDispatch<SwCutListItem>(subFeat);
                                cutList.SetParent(parent, refConf);
                                yield return cutList;
                            }
                        }
                    }

                    break;
                }
                else if (feat.GetTypeName2() == "RefPlane")
                {
                    break;
                }
            }

            yield break;
        }
    }
}