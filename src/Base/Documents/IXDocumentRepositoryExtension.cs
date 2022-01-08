﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using System.Linq;
using Xarial.XCad.Base;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Documents.Services;

namespace Xarial.XCad.Documents
{
    /// <summary>
    /// Additonal methods for <see cref="IXDocumentRepository"/>
    /// </summary>
    public static class IXDocumentRepositoryExtension
    {
        /// <summary>
        /// Creates new part document
        /// </summary>
        /// <param name="repo">This repository</param>
        /// <returns>New part</returns>
        public static IXPart NewPart(this IXDocumentRepository repo)
            => CreateAndCommitNewDocument<IXPart>(repo);

        /// <summary>
        /// Creates new assembly document
        /// </summary>
        /// <param name="repo">This repository</param>
        /// <returns>New aseembly</returns>
        public static IXAssembly NewAssembly(this IXDocumentRepository repo)
            => CreateAndCommitNewDocument<IXAssembly>(repo);

        /// <summary>
        /// Creates new drawing document
        /// </summary>
        /// <param name="repo">This repository</param>
        /// <returns>New drawing</returns>
        public static IXDrawing NewDrawing(this IXDocumentRepository repo)
            => CreateAndCommitNewDocument<IXDrawing>(repo);

        private static TDoc CreateAndCommitNewDocument<TDoc>(this IXDocumentRepository repo)
            where TDoc : class, IXDocument
        {
            var doc = repo.PreCreate<TDoc>();
            repo.Add(doc);

            if (doc is IXUnknownDocument) 
            {
                doc = (TDoc)(doc as IXUnknownDocument).GetSpecific();
            }

            return doc;
        }

        /// <summary>
        /// Opens the specified document
        /// </summary>
        /// <param name="repo">Documents repository</param>
        /// <param name="path">Path to document to open</param>
        /// <param name="state">State of the document</param>
        /// <returns>Opened document</returns>
        public static IXDocument Open(this IXDocumentRepository repo, string path, 
            DocumentState_e state = DocumentState_e.Default)
        {
            var doc = repo.PreCreate<IXUnknownDocument>();

            doc.Path = path;
            doc.State = state;

            repo.Add(doc);

            return doc.GetSpecific();
        }

        /// <summary>
        /// Registers document handler with the default constructor
        /// </summary>
        /// <typeparam name="THandler">Handler type</typeparam>
        /// <param name="repo">Documents repository</param>
        public static void RegisterHandler<THandler>(this IXDocumentRepository repo)
            where THandler : IDocumentHandler, new()
        {
            repo.RegisterHandler<THandler>(() => new THandler());
        }
    }
}