﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using Xarial.XCad.Base;
using Xarial.XCad.Documents.Delegates;

namespace Xarial.XCad.Documents
{
    /// <summary>
    /// Represents the collection of configurations in <see cref="IXDocument3D"/>
    /// </summary>
    public interface IXConfigurationRepository : IXRepository<IXConfiguration>
    {
        /// <summary>
        /// Fired when configuration is activated
        /// </summary>
        event ConfigurationActivatedDelegate ConfigurationActivated;

        /// <summary>
        /// Returns the currently active configuration or activates the specific configuration
        /// </summary>
        IXConfiguration Active { get; set; }

        /// <summary>
        /// Creates new template configuration
        /// </summary>
        /// <returns>Pre-created configuration</returns>
        IXConfiguration PreCreate();
    }
}
