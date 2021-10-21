﻿//*********************************************************************
//xCAD
//Copyright(C) 2021 Xarial Pty Limited
//Product URL: https://www.xcad.net
//License: https://xcad.xarial.com/license/
//*********************************************************************

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Xarial.XCad.Base.Enums;
using Xarial.XCad.Documents;
using Xarial.XCad.Geometry;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks.Enums;
using Xarial.XCad.SolidWorks.Geometry;
using Xarial.XCad.SolidWorks.Utils;
using Xarial.XCad.Toolkit.Windows;
using Xarial.XCad.Utils.Diagnostics;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using Xarial.XCad.SolidWorks.Exceptions;
using Xarial.XCad.Base;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Drawing;
using Xarial.XCad.Toolkit;
using Xarial.XCad.SolidWorks.Services;
using Xarial.XCad.Services;
using Xarial.XCad.Enums;
using Xarial.XCad.Delegates;
using Xarial.XCad.Base.Attributes;
using Xarial.XCad.UI;
using Xarial.XCad.SolidWorks.UI;
using Xarial.XCad.Reflection;

namespace Xarial.XCad.SolidWorks
{
    public interface ISwApplication : IXApplication, IDisposable
    {
        ISldWorks Sw { get; }
        new ISwVersion Version { get; set; }

        IXServiceCollection CustomServices { get; set; }

        new ISwDocumentCollection Documents { get; }
        new ISwMemoryGeometryBuilder MemoryGeometryBuilder { get; }
        new ISwMacro OpenMacro(string path);

        TObj CreateObjectFromDispatch<TObj>(object disp, ISwDocument doc)
            where TObj : ISwObject;
    }

    /// <inheritdoc/>
    internal class SwApplication : ISwApplication, IXServiceConsumer
    {
        #region WinApi
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        #endregion

        IXDocumentRepository IXApplication.Documents => Documents;
        IXMacro IXApplication.OpenMacro(string path) => OpenMacro(path);
        IXMemoryGeometryBuilder IXApplication.MemoryGeometryBuilder => MemoryGeometryBuilder;
        IXVersion IXApplication.Version
        {
            get => Version;
            set => Version = (ISwVersion)value;
        }

        public event ApplicationStartingDelegate Starting;
        public event ConfigureServicesDelegate ConfigureServices;

        public event ApplicationIdleDelegate Idle
        {
            add
            {
                if(m_IdleDelegate == null) 
                {
                    ((SldWorks)Sw).OnIdleNotify += OnIdleNotify;
                }

                m_IdleDelegate += value;
            }
            remove
            {
                m_IdleDelegate -= value;

                if (m_IdleDelegate == null)
                {
                    ((SldWorks)Sw).OnIdleNotify -= OnIdleNotify;
                }
            }
        }

        private int OnIdleNotify()
        {
            const int S_OK = 0;

            m_IdleDelegate?.Invoke(this);

            return S_OK;
        }

        private IXServiceCollection m_CustomServices;

        public ISldWorks Sw => m_Creator.Element;

        public ISwVersion Version 
        {
            get 
            {
                if (IsCommitted)
                {
                    return new SwVersion(Sw.GetVersion());
                }
                else 
                {
                    return m_Creator.CachedProperties.Get<SwVersion>();
                }
            }
            set 
            {
                if (IsCommitted)
                {
                    throw new Exception("Version cannot be changed after the application is committed");
                }
                else 
                {
                    m_Creator.CachedProperties.Set(value);
                }
            }
        }

        private SwDocumentCollection m_Documents;

        public ISwDocumentCollection Documents 
        {
            get 
            {
                m_Documents.Attach();
                return m_Documents;
            }
        }
        
        public IntPtr WindowHandle => new IntPtr(Sw.IFrameObject().GetHWndx64());

        public Process Process => Process.GetProcessById(Sw.GetProcessID());

        public Rectangle WindowRectangle => new Rectangle(Sw.FrameLeft, Sw.FrameTop, Sw.FrameWidth, Sw.FrameHeight);

        public ISwMemoryGeometryBuilder MemoryGeometryBuilder { get; private set; }

        public bool IsCommitted => m_Creator.IsCreated;

        public ApplicationState_e State 
        {
            get 
            {
                if (IsCommitted)
                {
                    return GetApplicationState();
                }
                else 
                {
                    return m_Creator.CachedProperties.Get<ApplicationState_e>();
                }
            }
            set 
            {
                if (IsCommitted)
                {
                    var curState = GetApplicationState();

                    if (curState == value)
                    {
                        //do nothing
                    }
                    else if (((int)curState - (int)value) == (int)ApplicationState_e.Hidden)
                    {
                        Sw.Visible = true;
                    }
                    else if ((int)value - ((int)curState) == (int)ApplicationState_e.Hidden)
                    {
                        Sw.Visible = false;
                    }
                    else 
                    {
                        throw new Exception("Only visibility can changed after the application is started");
                    }
                }
                else 
                {
                    m_Creator.CachedProperties.Set(value);
                }
            }
        }

        public IXServiceCollection CustomServices 
        {
            get => m_CustomServices;
            set 
            {
                if (!IsCommitted)
                {
                    m_CustomServices = value;
                }
                else 
                {
                    throw new Exception("Services can only be set before committing");
                }
            }
        }

        private IXLogger m_Logger;

        internal IServiceProvider Services { get; private set; }

        public bool IsAlive 
        {
            get
            {
                try
                {
                    if (Process == null || Process.HasExited || !Process.Responding)
                    {
                        return false;
                    }
                    else
                    {
                        var testCall = Sw.RevisionNumber();
                        return true;
                    }
                }
                catch 
                {
                    return false;
                }
            }
        }

        private bool m_IsInitialized;

        private bool m_HideOnStartup;
        
        private bool m_IsStartupNotified;

        private ElementCreator<ISldWorks> m_Creator;

        private ApplicationIdleDelegate m_IdleDelegate;

        private readonly Action<SwApplication> m_StartupCompletedCallback;

        internal SwApplication(ISldWorks app, IXServiceCollection customServices) 
            : this(app, default(Action<SwApplication>))
        {
            Init(customServices);
        }

        /// <summary>
        /// Only to be used within SwAddInEx
        /// </summary>
        internal SwApplication(ISldWorks app, Action<SwApplication> startupCompletedCallback)
        {
            m_IsStartupNotified = false;
            m_StartupCompletedCallback = startupCompletedCallback;

            m_Creator = new ElementCreator<ISldWorks>(CreateInstance, app, true);
            WatchStartupCompleted((SldWorks)app);
        }

        /// <summary>
        /// Remarks used for <see cref="SwApplicationFactory.PreCreate"/>
        /// </summary>
        internal SwApplication()
        {
            m_IsStartupNotified = false;

            m_Creator = new ElementCreator<ISldWorks>(CreateInstance, null, false);

            m_Creator.CachedProperties.Set(new ServiceCollection(), nameof(CustomServices));
        }
        
        internal void Init(IXServiceCollection customServices)
        {
            if (!m_IsInitialized)
            {
                m_CustomServices = customServices;

                m_IsInitialized = true;

                var services = new ServiceCollection();

                ConfigureServices?.Invoke(this, services);
                OnConfigureServices(services);

                if (customServices != null)
                {
                    services.Merge(customServices);
                }

                Services = services.CreateProvider();
                m_Logger = Services.GetService<IXLogger>();

                m_Documents = new SwDocumentCollection(this, m_Logger);

                var geomBuilderDocsProvider = Services.GetService<IMemoryGeometryBuilderDocumentProvider>();

                MemoryGeometryBuilder = new SwMemoryGeometryBuilder(this, geomBuilderDocsProvider);
            }
            else 
            {
                Debug.Assert(false, "App has been already initialized. Must be only once");
            }
        }

        public MessageBoxResult_e ShowMessageBox(string msg, MessageBoxIcon_e icon = MessageBoxIcon_e.Info, MessageBoxButtons_e buttons = MessageBoxButtons_e.Ok)
        {
            swMessageBoxBtn_e swBtn = 0;
            swMessageBoxIcon_e swIcon = 0;

            switch (icon)
            {
                case MessageBoxIcon_e.Info:
                    swIcon = swMessageBoxIcon_e.swMbInformation;
                    break;

                case MessageBoxIcon_e.Question:
                    swIcon = swMessageBoxIcon_e.swMbQuestion;
                    break;

                case MessageBoxIcon_e.Error:
                    swIcon = swMessageBoxIcon_e.swMbStop;
                    break;

                case MessageBoxIcon_e.Warning:
                    swIcon = swMessageBoxIcon_e.swMbWarning;
                    break;
            }

            switch (buttons)
            {
                case MessageBoxButtons_e.Ok:
                    swBtn = swMessageBoxBtn_e.swMbOk;
                    break;

                case MessageBoxButtons_e.YesNo:
                    swBtn = swMessageBoxBtn_e.swMbYesNo;
                    break;

                case MessageBoxButtons_e.OkCancel:
                    swBtn = swMessageBoxBtn_e.swMbOkCancel;
                    break;

                case MessageBoxButtons_e.YesNoCancel:
                    swBtn = swMessageBoxBtn_e.swMbYesNoCancel;
                    break;
            }

            var swRes = (swMessageBoxResult_e)Sw.SendMsgToUser2(msg, (int)swIcon, (int)swBtn);

            switch (swRes)
            {
                case swMessageBoxResult_e.swMbHitOk:
                    return MessageBoxResult_e.Ok;

                case swMessageBoxResult_e.swMbHitCancel:
                    return MessageBoxResult_e.Cancel;

                case swMessageBoxResult_e.swMbHitYes:
                    return MessageBoxResult_e.Yes;

                case swMessageBoxResult_e.swMbHitNo:
                    return MessageBoxResult_e.No;

                default:
                    return 0;
            }
        }

        public ISwMacro OpenMacro(string path)
        {
            const string VSTA_FILE_EXT = ".dll";
            const string VBA_FILE_EXT = ".swp";
            const string BASIC_EXT = ".swb";

            var ext = Path.GetExtension(path);

            switch (ext.ToLower()) 
            {
                case VSTA_FILE_EXT:
                    return new SwVstaMacro(this, path);

                case VBA_FILE_EXT:
                case BASIC_EXT:
                    return new SwVbaMacro(Sw, path);

                default:
                    throw new NotSupportedException("Specified file is not a SOLIDWORKS macro");
            }
        }

        public void Dispose()
        {
            try
            {
                m_Documents.Dispose();
            }
            catch (Exception ex)
            {
                m_Logger.Log(ex);
            }

            if (Sw != null)
            {
                if (Marshal.IsComObject(Sw))
                {
                    Marshal.ReleaseComObject(Sw);
                }
            }
        }

        public void Close()
        {
            Sw.ExitApp();
        }
        
        public void Commit(CancellationToken cancellationToken)
        {
            m_Creator.Create(cancellationToken);
            Init(CustomServices);
        }

        private ISldWorks CreateInstance(CancellationToken cancellationToken)
        {
            m_HideOnStartup = State.HasFlag(ApplicationState_e.Hidden);

            using (var appStarter = new SwApplicationStarter(State, Version)) 
            {
                var app = appStarter.Start(p => Starting?.Invoke(this, p), cancellationToken);
                WatchStartupCompleted((SldWorks)app);
                return app;
            }
        }

        private void WatchStartupCompleted(SldWorks sw) 
        {
            sw.OnIdleNotify += OnLoadFirstIdleNotify;
        }

        private int OnLoadFirstIdleNotify()
        {
            const int S_OK = 0;

            Debug.Assert(!m_IsStartupNotified, "This event shoud only be fired once");
            
            if (!m_IsStartupNotified)
            {
                if (Sw?.StartupProcessCompleted == true)
                {
                    if (m_HideOnStartup)
                    {
                        const int HIDE = 0;
                        ShowWindow(new IntPtr(Sw.IFrameObject().GetHWnd()), HIDE);

                        Sw.Visible = false;
                    }

                    m_IsStartupNotified = true;

                    m_StartupCompletedCallback?.Invoke(this);

                    if (Sw != null)
                    {
                        (Sw as SldWorks).OnIdleNotify -= OnLoadFirstIdleNotify;
                    }
                }
            }
            else
            {
                (Sw as SldWorks).OnIdleNotify -= OnLoadFirstIdleNotify;
            }

            return S_OK;
        }

        private ApplicationState_e GetApplicationState() 
        {
            //TODO: find the state
            return ApplicationState_e.Default;
        }

        public void OnConfigureServices(IXServiceCollection collection)
        {
            collection.AddOrReplace((Func<IXLogger>)(() => new TraceLogger("xCAD.SwApplication")));
            collection.AddOrReplace((Func<IMemoryGeometryBuilderDocumentProvider>)(() => new DefaultMemoryGeometryBuilderDocumentProvider(this)));
            collection.AddOrReplace((Func<IFilePathResolver>)(() => new SwFilePathResolverNoSearchFolders(this)));//TODO: there is some issue with recursive search of folders in search locations - do a test to validate
        }

        public IXProgress CreateProgress()
        {
            if (Sw.GetUserProgressBar(out UserProgressBar prgBar))
            {
                return new SwProgress(prgBar);
            }
            else 
            {
                throw new Exception("Failed to create progress");
            }
        }

        public void ShowTooltip(ITooltipSpec spec)
        {
            var bmp = "";
            IXImage icon = null;

            spec.GetType().TryGetAttribute<IconAttribute>(a => icon = a.Icon);

            var bmpType = icon != null ? swBitMaps.swBitMapUserDefined : swBitMaps.swBitMapNone;

            IIconsCreator iconsCreator = null;

            if (icon != null)
            {
                iconsCreator = Services.GetService<IIconsCreator>();

                bmp = iconsCreator.ConvertIcon(new TooltipIcon(icon)).First();
            }

            Sw.ShowBubbleTooltipAt2(spec.Position.X, spec.Position.Y, (int)spec.ArrowPosition,
                        spec.Title, spec.Message, (int)bmpType,
                        bmp, "", 0, (int)swLinkString.swLinkStringNone, "", "");

            iconsCreator?.Dispose();
        }

        public TObj CreateObjectFromDispatch<TObj>(object disp, ISwDocument doc)
            where TObj : ISwObject
            => SwObjectFactory.FromDispatch<TObj>(disp, doc, this);
    }

    public static class SwApplicationExtension 
    {
        public static bool IsVersionNewerOrEqual(this ISwApplication app, SwVersion_e version, 
            int? servicePack = null, int? servicePackRev = null) 
        {
            return app.Sw.IsVersionNewerOrEqual(version, servicePack, servicePackRev);
        }
    }
}