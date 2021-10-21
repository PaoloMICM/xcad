﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xarial.XCad.Base;
using Xarial.XCad.Base.Enums;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Delegates;
using Xarial.XCad.Documents.Exceptions;
using Xarial.XCad.Documents.Services;
using Xarial.XCad.Documents.Structures;
using Xarial.XCad.Exceptions;
using Xarial.XCad.SolidWorks.Documents.Services;
using Xarial.XCad.SolidWorks.Utils;
using Xarial.XCad.Toolkit.Services;
using Xarial.XCad.Utils.Diagnostics;

namespace Xarial.XCad.SolidWorks.Documents
{
    public interface ISwDocumentCollection : IXDocumentRepository, IDisposable
    {
        new ISwDocument Active { get; set; }
        new IXDocument this[string name] { get; }
        ISwDocument this[IModelDoc2 model] { get; }
    }

    [DebuggerDisplay("Documents: {" + nameof(Count) + "}")]
    internal class SwDocumentCollection : ISwDocumentCollection
    {
        public event DocumentEventDelegate DocumentLoaded;

        public event DocumentEventDelegate DocumentActivated 
        {
            add 
            {
                if (m_DocumentActivated == null) 
                {
                    m_SwApp.ActiveModelDocChangeNotify += OnActiveModelDocChangeNotify;
                }

                m_DocumentActivated += value;
            }
            remove 
            {
                m_DocumentActivated -= value;

                if (m_DocumentActivated == null)
                {
                    m_SwApp.ActiveModelDocChangeNotify -= OnActiveModelDocChangeNotify;
                }
            }
        }

        public event DocumentEventDelegate NewDocumentCreated
        {
            add
            {
                if (m_NewDocumentCreated == null)
                {
                    m_SwApp.FileNewNotify2 += OnFileNewNotify;
                }

                m_NewDocumentCreated += value;
            }
            remove
            {
                m_NewDocumentCreated -= value;

                if (m_NewDocumentCreated == null)
                {
                    m_SwApp.FileNewNotify2 -= OnFileNewNotify;
                }
            }
        }

        public event DocumentEventDelegate DocumentOpened
        {
            add
            {
                if (m_DocumentOpened == null)
                {
                    m_SwApp.FileOpenPostNotify += OnFileOpenPostNotify;
                }

                m_DocumentOpened += value;
            }
            remove
            {
                m_DocumentOpened -= value;

                if (m_DocumentOpened == null)
                {
                    m_SwApp.FileOpenPostNotify -= OnFileOpenPostNotify;
                }
            }
        }


        IXDocument IXDocumentRepository.Active 
        {
            get => Active;
            set => Active = (SwDocument)value;
        }
        
        private const int S_OK = 0;

        private readonly SwApplication m_App;
        private readonly SldWorks m_SwApp;
        private readonly Dictionary<IModelDoc2, SwDocument> m_Documents;
        private readonly IXLogger m_Logger;
        private readonly DocumentsHandler m_DocsHandler;

        private readonly SwDocumentDispatcher m_DocsDispatcher;

        private DocumentEventDelegate m_DocumentActivated;
        private DocumentEventDelegate m_DocumentOpened;
        private DocumentEventDelegate m_NewDocumentCreated;

        private IModelDoc2 m_WaitActivateDocument;
        private IModelDoc2 m_WaitOpenDocument;
        private IModelDoc2 m_WaitNewDocument;

        private object m_Lock;

        public ISwDocument Active
        {
            get
            {
                var activeDoc = m_SwApp.IActiveDoc2;

                if (activeDoc != null)
                {
                    return this[activeDoc];
                }
                else
                {
                    return null;
                }
            }
            set 
            {
                int errors = -1;
                var doc = m_SwApp.ActivateDoc3(value.Title, true, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref errors);

                if (doc == null) 
                {
                    throw new Exception($"Failed to activate the document. Error code: {errors}");
                }
            }
        }

        public int Count
        {
            get
            {
                ClearDanglingModelPointers(false);

                return m_Documents.Count;
            }
        }

        public IXDocument this[string name] 
        {
            get 
            {
                if (TryGet(name, out IXDocument doc))
                {
                    return doc;
                }
                else 
                {
                    throw new EntityNotFoundException(name);
                }
            }
        }

        private bool m_IsAttached;

        private readonly List<IModelDoc2> m_DanglingModelPointers;

        internal SwDocumentCollection(SwApplication app, IXLogger logger)
        {
            m_Lock = new object();

            m_App = app;
            m_SwApp = (SldWorks)m_App.Sw;
            m_Logger = logger;

            m_DocsDispatcher = new SwDocumentDispatcher(app, logger);

            m_DanglingModelPointers = new List<IModelDoc2>();

            m_Documents = new Dictionary<IModelDoc2, SwDocument>(
                new SwModelPointerEqualityComparer(m_DanglingModelPointers));
            m_DocsHandler = new DocumentsHandler(app, m_Logger);

            m_IsAttached = false;
        }

        internal void Attach() 
        {
            if (!m_IsAttached)
            {
                m_IsAttached = true;

                m_DocsDispatcher.Dispatched += OnDocumentDispatched;

                AttachToAllOpenedDocuments();

                m_SwApp.DocumentLoadNotify2 += OnDocumentLoadNotify2;
            }
        }
        
        private int OnActiveModelDocChangeNotify()
        {
            var activeDoc = m_SwApp.IActiveDoc2;

            if (m_Documents.TryGetValue(activeDoc, out SwDocument doc))
            {
                try
                {
                    m_DocumentActivated?.Invoke(doc);
                }
                catch (Exception ex)
                {
                    m_Logger.Log(ex);
                }
            }
            else
            {
                m_Logger.Log($"Adding {activeDoc.GetTitle()} to the activate waiting list", LoggerMessageSeverity_e.Debug);

                //activate event can happen before the loading event, so the document is not yet registered
                m_WaitActivateDocument = activeDoc;
            }

            return S_OK;
        }

        public ISwDocument this[IModelDoc2 model]
        {
            get
            {
                SwDocument doc;

                if (m_Documents.TryGetValue(model, out doc))
                {
                    return doc;
                }
                else
                {
                    throw new KeyNotFoundException("Specified model document is not registered");
                }
            }
        }

        public IEnumerator<IXDocument> GetEnumerator()
        {
            ClearDanglingModelPointers(false);
            return m_Documents.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private int OnFileOpenPostNotify(string fileName)
        {
            if (TryFindExistingDocumentByPath(fileName, out SwDocument doc))
            {
                try
                {
                    m_DocumentOpened?.Invoke(doc);
                }
                catch (Exception ex)
                {
                    m_Logger.Log(ex);
                }
            }
            else
            {
                m_Logger.Log($"Adding {fileName} to the open waiting list", LoggerMessageSeverity_e.Debug);

                m_WaitOpenDocument = (IModelDoc2)m_SwApp.GetOpenDocumentByName(fileName);

                if (m_WaitOpenDocument == null) 
                {
                    m_Logger.Log($"Failed to find the document by name '{fileName}'", LoggerMessageSeverity_e.Error);
                }
            }

            return S_OK;
        }

        private int OnFileNewNotify(object newDoc, int docType, string templateName)
        {
            var model = (IModelDoc2)newDoc;

            if (m_Documents.TryGetValue(model, out SwDocument doc))
            {
                try
                {
                    m_NewDocumentCreated?.Invoke(doc);
                }
                catch (Exception ex)
                {
                    m_Logger.Log(ex);
                }
            }
            else
            {
                m_Logger.Log($"Adding {model.GetTitle()} to the new document waiting list", LoggerMessageSeverity_e.Debug);

                m_WaitNewDocument = model;
            }

            return S_OK;
        }

        private void AttachToAllOpenedDocuments()
        {
            var openDocs = m_SwApp.GetDocuments() as object[];

            if (openDocs != null)
            {
                foreach (IModelDoc2 model in openDocs)
                {
                    m_DocsDispatcher.Dispatch(model.GetTitle(), model.GetPathName());
                }
            }
        }

        private void OnDocumentDispatched(SwDocument doc)
        {
            lock (m_Lock)
            {
                if (!m_Documents.ContainsKey(doc.Model))
                {
                    ClearDanglingModelPointers(true);

                    doc.Destroyed += OnDocumentDestroyed;

                    m_Documents.Add(doc.Model, doc);

                    m_DocsHandler.TryInitHandlers(doc);

                    try
                    {
                        DocumentLoaded?.Invoke(doc);
                    }
                    catch (Exception ex)
                    {
                        m_Logger.Log(ex);
                    }

                    if (m_WaitActivateDocument != null
                        && SwModelPointerEqualityComparer.AreEqual(m_WaitActivateDocument, doc.Model))
                    {
                        try
                        {
                            m_Logger.Log($"Calling document activated event for the {doc.Title}", LoggerMessageSeverity_e.Debug);
                            m_DocumentActivated?.Invoke(doc);
                        }
                        catch (Exception ex)
                        {
                            m_Logger.Log(ex);
                        }

                        m_WaitActivateDocument = null;
                    }

                    if (m_WaitNewDocument != null
                        && SwModelPointerEqualityComparer.AreEqual(m_WaitNewDocument, doc.Model))
                    {
                        try
                        {
                            m_Logger.Log($"Calling new document created event for the {doc.Title}", LoggerMessageSeverity_e.Debug);
                            m_NewDocumentCreated?.Invoke(doc);
                        }
                        catch (Exception ex)
                        {
                            m_Logger.Log(ex);
                        }

                        m_WaitNewDocument = null;
                    }

                    if (m_WaitOpenDocument != null
                        && SwModelPointerEqualityComparer.AreEqual(m_WaitOpenDocument, doc.Model))
                    {
                        try
                        {
                            m_Logger.Log($"Calling document opened event for the {doc.Title}", LoggerMessageSeverity_e.Debug);
                            m_DocumentOpened?.Invoke(doc);
                        }
                        catch (Exception ex)
                        {
                            m_Logger.Log(ex);
                        }

                        m_WaitOpenDocument = null;
                    }
                }
                else 
                {
                    //NOTE: this happens on activation of the document
                    m_Logger.Log($"Conflict. {doc.Model.GetTitle()} already dispatched", LoggerMessageSeverity_e.Warning);
                    //Debug.Assert(false, "Document already dispatched");
                }
            }
        }

        private void ClearDanglingModelPointers(bool cachedOnly)
        {
            lock (m_Lock)
            {
                void RemovePointers(IEnumerable<IModelDoc2> models) 
                {
                    foreach (var dangModelPtr in models)
                    {
                        try
                        {
                            m_Logger.Log("Removing dangling model from the list");
                            m_Documents.Remove(dangModelPtr);
                        }
                        catch (Exception ex)
                        {
                            m_Logger.Log(ex);
                        }
                    }
                }

                if (m_DanglingModelPointers.Count > 0)
                {
                    RemovePointers(m_DanglingModelPointers);

                    m_DanglingModelPointers.Clear();
                }

                if (!cachedOnly)
                {
                    if (m_SwApp.GetDocumentCount() != m_Documents.Count)
                    {
                        var dangPtrs = m_Documents.Where(d => !d.Value.IsAlive).Select(d => d.Key).ToArray();

                        RemovePointers(dangPtrs);
                    }
                }
            }
        }

        private int OnDocumentLoadNotify2(string docTitle, string docPath)
        {
            m_DocsDispatcher.Dispatch(docTitle, docPath);
            
            return S_OK;
        }

        private void OnDocumentDestroyed(SwDocument doc)
            => ReleaseDocument(doc);

        private void ReleaseDocument(SwDocument doc)
        {
            doc.Destroyed -= OnDocumentDestroyed;
            m_Documents.Remove(doc.Model);
            m_DocsHandler.ReleaseHandlers(doc);
            doc.SetClosed();
            doc.Dispose();

            ClearDanglingModelPointers(true);
        }

        public void RegisterHandler<THandler>(Func<THandler> handlerFact) 
            where THandler : IDocumentHandler
            => m_DocsHandler.RegisterHandler(handlerFact);

        public THandler GetHandler<THandler>(IXDocument doc) 
            where THandler : IDocumentHandler
            => m_DocsHandler.GetHandler<THandler>(doc);

        public TDocument PreCreate<TDocument>()
             where TDocument : class, IXDocument
        {
            SwDocument templateDoc;

            if (typeof(IXPart).IsAssignableFrom(typeof(TDocument)))
            {
                templateDoc = new SwPart(null, m_App, m_Logger, false);
            }
            else if (typeof(IXAssembly).IsAssignableFrom(typeof(TDocument)))
            {
                templateDoc = new SwAssembly(null, m_App, m_Logger, false);
            }
            else if (typeof(IXDrawing).IsAssignableFrom(typeof(TDocument)))
            {
                templateDoc = new SwDrawing(null, m_App, m_Logger, false);
            }
            else if (typeof(IXDocument3D).IsAssignableFrom(typeof(TDocument)))
            {
                templateDoc = new SwUnknownDocument3D(null, m_App, m_Logger, false);
            }
            else if (typeof(IXDocument).IsAssignableFrom(typeof(TDocument)) 
                || typeof(IXUnknownDocument).IsAssignableFrom(typeof(TDocument)))
            {
                templateDoc = new SwUnknownDocument(null, m_App, m_Logger, false);
            }
            else
            {
                throw new NotSupportedException("Creation of this type of document is not supported");
            }

            templateDoc.SetDispatcher(m_DocsDispatcher);

            if (templateDoc is TDocument)
            {
                return templateDoc as TDocument;
            }
            else 
            {
                throw new InvalidCastException($"{templateDoc.GetType().FullName} cannot be cast to {typeof(TDocument).FullName}");
            }
        }

        public bool TryGet(string name, out IXDocument ent)
        {
            var model = m_SwApp.GetOpenDocumentByName(name) as IModelDoc2;
            ent = null;

            if (model != null)
            {
                SwDocument doc;

                if (m_Documents.TryGetValue(model, out doc))
                {
                    ent = doc;
                    return true;
                }
            }

            return false;
        }

        public void AddRange(IEnumerable<IXDocument> ents)
        {
            foreach (SwDocument doc in ents) 
            {
                doc.Commit();
            }
        }

        public void RemoveRange(IEnumerable<IXDocument> ents)
        {
            foreach (var doc in ents.ToArray()) 
            {
                doc.Close();
            }
        }

        internal bool TryFindExistingDocumentByPath(string path, out SwDocument doc)
        {
            doc = (SwDocument)this.FirstOrDefault(
                d => string.Equals(d.Path, path, StringComparison.CurrentCultureIgnoreCase));

            return doc != null;
        }

        public void Dispose()
        {
            if (m_IsAttached)
            {
                m_DocsDispatcher.Dispatched -= OnDocumentDispatched;
                m_SwApp.DocumentLoadNotify2 -= OnDocumentLoadNotify2;
                m_SwApp.ActiveModelDocChangeNotify -= OnActiveModelDocChangeNotify;
                m_SwApp.FileNewNotify2 -= OnFileNewNotify;
                m_SwApp.FileOpenPostNotify -= OnFileOpenPostNotify;

                foreach (var doc in m_Documents.Values.ToArray())
                {
                    ReleaseDocument(doc);
                }

                m_Documents.Clear();
            }
        }
    }

    /// <summary>
    /// Additional methods for documents collections
    /// </summary>
    public static class SwDocumentCollectionExtension 
    {
        /// <summary>
        /// Pre creates new document from path
        /// </summary>
        /// <param name="docsColl">Documents collection</param>
        /// <param name="path"></param>
        /// <returns>Pre-created document</returns>
        public static ISwDocument PreCreateFromPath(this ISwDocumentCollection docsColl, string path)
        {
            var ext = Path.GetExtension(path);

            ISwDocument doc;

            switch (ext.ToLower())
            {
                case ".sldprt":
                case ".sldblk":
                case ".prtdot":
                case ".sldlfp":
                    doc = docsColl.PreCreate<ISwPart>();
                    break;

                case ".sldasm":
                case ".asmdot":
                    doc = docsColl.PreCreate<ISwAssembly>();
                    break;

                case ".slddrw":
                case ".drwdot":
                    doc = docsColl.PreCreate<ISwDrawing>();
                    break;

                default:
                    throw new NotSupportedException("Only native documents are supported");
            }

            doc.Path = path;

            return doc;
        }
    }
}